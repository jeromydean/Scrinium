using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using Scrinium.Core.Ports;
using UglyToad.PdfPig;

namespace Scrinium.Infrastructure.Extraction;

public sealed class DocumentExtractor : IDocumentExtractor
{
  private readonly TikaMetadataClient _tika;
  private readonly ILogger<DocumentExtractor> _logger;

  public DocumentExtractor(
    TikaMetadataClient tika,
    IFormatRouter formatRouter,
    ILogger<DocumentExtractor> logger)
  {
    _tika = tika;
    _ = formatRouter;
    _logger = logger;
  }

  public Task<Dictionary<string, string>> ExtractPdfMetadataAsync(
    byte[] pdfBytes,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    try
    {
      using PdfDocument document = PdfDocument.Open(pdfBytes);
      Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

      if (document.Information is not null)
      {
        AddIfPresent(metadata, "title", document.Information.Title);
        AddIfPresent(metadata, "author", document.Information.Author);
        AddIfPresent(metadata, "subject", document.Information.Subject);
        AddIfPresent(metadata, "keywords", document.Information.Keywords);
        AddIfPresent(metadata, "creator", document.Information.Creator);
        AddIfPresent(metadata, "producer", document.Information.Producer);
      }

      return Task.FromResult(metadata);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "PdfPig metadata extraction failed.");
      throw new InvalidOperationException($"PDF metadata extraction failed: {ex.Message}", ex);
    }
  }

  public int GetPdfPageCount(byte[] pdfBytes)
  {
    if (pdfBytes.Length == 0)
    {
      return 0;
    }

    try
    {
      using PdfDocument document = PdfDocument.Open(pdfBytes);
      return Math.Max(document.NumberOfPages, 1);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "PdfPig page count failed; falling back to PDFtoImage.");
      return Math.Max(Conversion.GetPageCount(pdfBytes), 1);
    }
  }

  public Task<Dictionary<string, string>> ExtractTikaMetadataAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
    => SafeTikaMetadataAsync(content, contentType, cancellationToken);

  public Task<string> ExtractTikaTextAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
    => SafeTikaTextAsync(content, contentType, cancellationToken);

  private async Task<Dictionary<string, string>> SafeTikaMetadataAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
  {
    try
    {
      return await _tika.ExtractMetadataAsync(content, contentType, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Tika metadata extraction failed for {ContentType}.", contentType);
      return new Dictionary<string, string>();
    }
  }

  private async Task<string> SafeTikaTextAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken)
  {
    try
    {
      return await _tika.ExtractTextAsync(content, contentType, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Tika text extraction failed for {ContentType}.", contentType);
      return string.Empty;
    }
  }

  private static void AddIfPresent(Dictionary<string, string> metadata, string key, string? value)
  {
    if (!string.IsNullOrWhiteSpace(value))
    {
      metadata[key] = value;
    }
  }
}
