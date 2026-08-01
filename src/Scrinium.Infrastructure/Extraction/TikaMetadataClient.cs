using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Extraction;
using Scrinium.Infrastructure.Options;

namespace Scrinium.Infrastructure.Extraction;

public sealed class TikaMetadataClient
{
  private readonly HttpClient _httpClient;
  private readonly TikaOptions _options;
  private readonly ILogger<TikaMetadataClient> _logger;

  public TikaMetadataClient(
    HttpClient httpClient,
    IOptions<TikaOptions> options,
    ILogger<TikaMetadataClient> logger)
  {
    _httpClient = httpClient;
    _options = options.Value;
    _logger = logger;
    _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
  }

  public async Task<Dictionary<string, string>> ExtractMetadataAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
  {
    using ByteArrayContent body = new(content);
    body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

    using HttpRequestMessage request = new(HttpMethod.Put, "meta")
    {
      Content = body,
    };
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      _logger.LogWarning(
        "Tika metadata extraction failed with status {StatusCode}.",
        (int)response.StatusCode);
      return new Dictionary<string, string>();
    }

    string json = await response.Content.ReadAsStringAsync(cancellationToken);
    return ParseMetadataJson(json);
  }

  public async Task<string> ExtractTextAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
  {
    using ByteArrayContent body = new(content);
    body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

    using HttpRequestMessage request = new(HttpMethod.Put, "tika")
    {
      Content = body,
    };
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      _logger.LogWarning(
        "Tika text extraction failed with status {StatusCode}.",
        (int)response.StatusCode);
      return string.Empty;
    }

    return await response.Content.ReadAsStringAsync(cancellationToken);
  }

  private static Dictionary<string, string> ParseMetadataJson(string json)
  {
    Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

    try
    {
      using JsonDocument document = JsonDocument.Parse(json);
      foreach (JsonProperty property in document.RootElement.EnumerateObject())
      {
        metadata[property.Name] = property.Value.ValueKind switch
        {
          JsonValueKind.String => property.Value.GetString() ?? string.Empty,
          JsonValueKind.Number => property.Value.GetRawText(),
          JsonValueKind.True => "true",
          JsonValueKind.False => "false",
          JsonValueKind.Array => string.Join(
            ", ",
            property.Value.EnumerateArray().Select(x => x.ToString())),
          _ => property.Value.GetRawText(),
        };
      }
    }
    catch (JsonException)
    {
      metadata["raw"] = json;
    }

    return metadata;
  }
}
