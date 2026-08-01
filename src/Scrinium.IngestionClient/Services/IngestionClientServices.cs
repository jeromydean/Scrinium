using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.IngestionClient.Api;
using Scrinium.IngestionClient.Options;

namespace Scrinium.IngestionClient.Services;

public sealed class KeycloakTokenProvider
{
  private readonly HttpClient _httpClient;
  private readonly KeycloakClientOptions _options;
  private readonly ILogger<KeycloakTokenProvider> _logger;
  private readonly SemaphoreSlim _lock = new(1, 1);
  private string? _accessToken;
  private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

  public KeycloakTokenProvider(
    HttpClient httpClient,
    IOptions<KeycloakClientOptions> options,
    ILogger<KeycloakTokenProvider> logger)
  {
    _httpClient = httpClient;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
  {
    if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
    {
      return _accessToken;
    }

    await _lock.WaitAsync(cancellationToken);
    try
    {
      if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
      {
        return _accessToken;
      }

      if (string.IsNullOrWhiteSpace(_options.ClientSecret))
      {
        throw new InvalidOperationException(
          "Keycloak ClientSecret is not configured. Run .\\docker\\setup-keycloak.ps1 and set Keycloak:ClientSecret or SCRINIUM_API_CLIENT_SECRET.");
      }

      using FormUrlEncodedContent form = new(new Dictionary<string, string>
      {
        ["grant_type"] = "password",
        ["client_id"] = _options.ClientId,
        ["client_secret"] = _options.ClientSecret,
        ["username"] = _options.Username,
        ["password"] = _options.Password,
        ["scope"] = _options.Scope,
      });

      HttpResponseMessage response = await _httpClient.PostAsync(_options.TokenUrl, form, cancellationToken);
      string body = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        throw new InvalidOperationException(
          $"Keycloak token request failed ({(int)response.StatusCode}): {body}");
      }

      TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(body);
      if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
      {
        throw new InvalidOperationException("Keycloak token response did not include access_token.");
      }

      _accessToken = token.AccessToken;
      _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn, 60));
      _logger.LogDebug("Obtained Keycloak access token (expires in {ExpiresIn}s).", token.ExpiresIn);
      return _accessToken;
    }
    finally
    {
      _lock.Release();
    }
  }
}

public sealed class IngestionApiClient
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  private readonly HttpClient _httpClient;
  private readonly ApiOptions _apiOptions;
  private readonly KeycloakTokenProvider _tokenProvider;
  private readonly ILogger<IngestionApiClient> _logger;

  public IngestionApiClient(
    HttpClient httpClient,
    IOptions<ApiOptions> apiOptions,
    KeycloakTokenProvider tokenProvider,
    ILogger<IngestionApiClient> logger)
  {
    _httpClient = httpClient;
    _apiOptions = apiOptions.Value;
    _tokenProvider = tokenProvider;
    _logger = logger;
  }

  public async Task<IngestionAcceptedResponse> UploadAsync(
    string filePath,
    IEnumerable<string> tags,
    CancellationToken cancellationToken)
  {
    string token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
    string fileName = Path.GetFileName(filePath);

    using MultipartFormDataContent form = new();
    await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    StreamContent fileContent = new(fileStream);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
    form.Add(fileContent, "file", fileName);

    foreach (string tag in tags)
    {
      if (!string.IsNullOrWhiteSpace(tag))
      {
        form.Add(new StringContent(tag), "tags");
      }
    }

    form.Add(new StringContent(fileName), "clientReference");
    form.Add(new StringContent("folder-watcher"), "metadata[source]");

    using HttpRequestMessage request = new(HttpMethod.Post, $"{_apiOptions.BaseUrl.TrimEnd('/')}/api/ingestion")
    {
      Content = form,
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    string body = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"Upload failed ({(int)response.StatusCode}) for {fileName}: {body}");
    }

    IngestionAcceptedResponse? accepted = JsonSerializer.Deserialize<IngestionAcceptedResponse>(body, JsonOptions);
    if (accepted is null)
    {
      throw new InvalidOperationException($"Upload response for {fileName} was not valid JSON.");
    }

    _logger.LogInformation(
      "Uploaded {FileName} as archive {ArchiveId} (status={Status}).",
      fileName,
      accepted.ArchiveId,
      accepted.Status);

    return accepted;
  }

  public async Task<BundleStatusResponse?> TryGetDefaultBundleAsync(
    Guid archiveId,
    CancellationToken cancellationToken)
  {
    string token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

    using HttpRequestMessage request = new(
      HttpMethod.Get,
      $"{_apiOptions.BaseUrl.TrimEnd('/')}/api/bundles/by-archive/{archiveId:D}");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }

    response.EnsureSuccessStatusCode();

    return await response.Content.ReadFromJsonAsync<BundleStatusResponse>(
      JsonOptions,
      cancellationToken);
  }

  public async Task WaitForReadyAsync(
    Guid archiveId,
    int pollIntervalMs,
    int timeoutSeconds,
    CancellationToken cancellationToken)
  {
    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
    BundleStatusResponse? last = null;

    while (DateTimeOffset.UtcNow < deadline)
    {
      last = await TryGetDefaultBundleAsync(archiveId, cancellationToken);
      if (last is null)
      {
        _logger.LogInformation(
          "Archive {ArchiveId}: default bundle not created yet.",
          archiveId);
      }
      else
      {
        _logger.LogInformation(
          "Archive {ArchiveId} bundle {BundleId}: status={Status}, sheets={SheetCount}, failed={SheetsFailedCount}",
          archiveId,
          last.BundleId,
          last.Status,
          last.SheetCount,
          last.SheetsFailedCount);

        if (last.Status is "ready" or "error")
        {
          return;
        }
      }

      await Task.Delay(pollIntervalMs, cancellationToken);
    }

    throw new TimeoutException(
      $"Archive {archiveId} default bundle did not reach a terminal state within {timeoutSeconds}s (last status={last?.Status}).");
  }

  public async Task DownloadPreviewAsync(
    Guid bundleId,
    Guid archiveSheetId,
    string outputPath,
    CancellationToken cancellationToken)
  {
    string token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

    using HttpRequestMessage request = new(
      HttpMethod.Get,
      $"{_apiOptions.BaseUrl.TrimEnd('/')}/api/bundles/{bundleId:D}/sheets/{archiveSheetId:D}/render?tier=preview");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    response.EnsureSuccessStatusCode();

    string? directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
      Directory.CreateDirectory(directory);
    }

    await using FileStream output = File.Create(outputPath);
    await response.Content.CopyToAsync(output, cancellationToken);

    _logger.LogInformation(
      "Saved preview for bundle {BundleId} sheet {ArchiveSheetId} to {OutputPath}.",
      bundleId,
      archiveSheetId,
      outputPath);
  }

  private static string GetContentType(string fileName)
  {
    string extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension switch
    {
      ".pdf" => "application/pdf",
      ".png" => "image/png",
      ".jpg" or ".jpeg" => "image/jpeg",
      ".tif" or ".tiff" => "image/tiff",
      ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      ".doc" => "application/msword",
      ".html" or ".htm" => "text/html",
      _ => "application/octet-stream",
    };
  }
}
