using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Models;
using Scrinium.Options;

namespace Scrinium.Services;

public sealed class ScriniumApiClient
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  private readonly HttpClient _httpClient;
  private readonly ApiOptions _options;
  private readonly IAccessTokenProvider _tokenProvider;

  public ScriniumApiClient(
    HttpClient httpClient,
    ApiOptions options,
    IAccessTokenProvider tokenProvider)
  {
    _httpClient = httpClient;
    _options = options;
    _tokenProvider = tokenProvider;
  }

  public async Task<BundleListResponse> ListBundlesAsync(
    string? status = null,
    CancellationToken cancellationToken = default)
  {
    string url = $"{_options.BaseUrl.TrimEnd('/')}/api/bundles?take=100";
    if (!string.IsNullOrWhiteSpace(status))
    {
      url += $"&status={Uri.EscapeDataString(status)}";
    }

    using HttpRequestMessage request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
    using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    await EnsureSuccessAsync(response, cancellationToken);

    BundleListResponse? list = await response.Content.ReadFromJsonAsync<BundleListResponse>(
      JsonOptions,
      cancellationToken);

    return list ?? new BundleListResponse();
  }

  public async Task<BundleDetailResponse?> GetBundleStatusAsync(
    Guid bundleId,
    CancellationToken cancellationToken = default)
  {
    string url = $"{_options.BaseUrl.TrimEnd('/')}/api/bundles/{bundleId}/status";
    using HttpRequestMessage request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
    using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }

    await EnsureSuccessAsync(response, cancellationToken);
    return await response.Content.ReadFromJsonAsync<BundleDetailResponse>(JsonOptions, cancellationToken);
  }

  public async Task<Stream> GetSheetRenderAsync(
    Guid bundleId,
    Guid archiveSheetId,
    string tier = "preview",
    CancellationToken cancellationToken = default)
  {
    string url =
      $"{_options.BaseUrl.TrimEnd('/')}/api/bundles/{bundleId}/sheets/{archiveSheetId}/render?tier={tier}";

    using HttpRequestMessage request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
    HttpResponseMessage response = await _httpClient.SendAsync(
      request,
      HttpCompletionOption.ResponseHeadersRead,
      cancellationToken);

    await EnsureSuccessAsync(response, cancellationToken);
    return await response.Content.ReadAsStreamAsync(cancellationToken);
  }

  private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
    HttpMethod method,
    string url,
    CancellationToken cancellationToken)
  {
    string token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
    HttpRequestMessage request = new(method, url);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return request;
  }

  private static async Task EnsureSuccessAsync(
    HttpResponseMessage response,
    CancellationToken cancellationToken)
  {
    if (response.IsSuccessStatusCode)
    {
      return;
    }

    string body = await response.Content.ReadAsStringAsync(cancellationToken);
    throw new InvalidOperationException(
      $"API request failed ({(int)response.StatusCode}): {body}");
  }
}
