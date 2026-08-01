using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ports;
using Scrinium.Infrastructure.Options;

namespace Scrinium.Infrastructure.Extraction;

public sealed class GotenbergNormalizer : IDocumentNormalizer
{
  private readonly HttpClient _httpClient;
  private readonly IFormatRouter _formatRouter;
  private readonly GotenbergOptions _options;
  private readonly ILogger<GotenbergNormalizer> _logger;

  public GotenbergNormalizer(
    HttpClient httpClient,
    IFormatRouter formatRouter,
    IOptions<GotenbergOptions> options,
    ILogger<GotenbergNormalizer> logger)
  {
    _httpClient = httpClient;
    _formatRouter = formatRouter;
    _options = options.Value;
    _logger = logger;
    _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
  }

  public async Task<NormalizeResult> NormalizeAsync(
    byte[] content,
    string contentType,
    string fileName,
    CancellationToken cancellationToken)
  {
    DocumentFormatKind kind = _formatRouter.Classify(contentType);

    if (kind is DocumentFormatKind.Pdf)
    {
      return new NormalizeResult
      {
        PdfBytes = content,
        WasConverted = false,
        ContentType = "application/pdf",
      };
    }

    if (kind is DocumentFormatKind.Image)
    {
      return new NormalizeResult
      {
        PdfBytes = [],
        WasConverted = false,
        ContentType = contentType,
      };
    }

    string endpoint = kind is DocumentFormatKind.Web
      ? "forms/chromium/convert/html"
      : "forms/libreoffice/convert";

    using MultipartFormDataContent form = new();
    ByteArrayContent fileContent = new(content);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
    form.Add(fileContent, "files", fileName);

    if (kind is DocumentFormatKind.Web)
    {
      form.Add(new StringContent("true"), "printBackground");
    }

    HttpResponseMessage response = await _httpClient.PostAsync(endpoint, form, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      string body = await response.Content.ReadAsStringAsync(cancellationToken);
      throw new InvalidOperationException(
        $"Gotenberg conversion failed ({(int)response.StatusCode}): {body}");
    }

    byte[] pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    _logger.LogInformation(
      "Converted {FileName} ({ContentType}) to PDF ({ByteCount} bytes).",
      fileName,
      contentType,
      pdfBytes.Length);

    return new NormalizeResult
    {
      PdfBytes = pdfBytes,
      WasConverted = true,
      ContentType = "application/pdf",
    };
  }
}
