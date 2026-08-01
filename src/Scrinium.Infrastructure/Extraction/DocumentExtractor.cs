using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ports;
using Scrinium.Infrastructure.Options;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ZXing;
using ZXing.SkiaSharp;

namespace Scrinium.Infrastructure.Extraction;

public sealed class DocumentExtractor : IDocumentExtractor
{
  private readonly TikaMetadataClient _tika;
  private readonly IFormatRouter _formatRouter;
  private readonly RenderingOptions _renderingOptions;
  private readonly ILogger<DocumentExtractor> _logger;

  public DocumentExtractor(
    TikaMetadataClient tika,
    IFormatRouter formatRouter,
    IOptions<RenderingOptions> renderingOptions,
    ILogger<DocumentExtractor> logger)
  {
    _tika = tika;
    _formatRouter = formatRouter;
    _renderingOptions = renderingOptions.Value;
    _logger = logger;
  }

  public Task<ExtractionResult> ExtractFromPdfAsync(
    byte[] pdfBytes,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    using PdfDocument document = PdfDocument.Open(pdfBytes);
    StringBuilder textBuilder = new();
    Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

    foreach (Page page in document.GetPages())
    {
      string pageText = page.Text;
      if (!string.IsNullOrWhiteSpace(pageText))
      {
        textBuilder.AppendLine(pageText);
      }
    }

    if (document.Information is not null)
    {
      AddIfPresent(metadata, "title", document.Information.Title);
      AddIfPresent(metadata, "author", document.Information.Author);
      AddIfPresent(metadata, "subject", document.Information.Subject);
      AddIfPresent(metadata, "keywords", document.Information.Keywords);
      AddIfPresent(metadata, "creator", document.Information.Creator);
      AddIfPresent(metadata, "producer", document.Information.Producer);
    }

    int pageCount = document.NumberOfPages;
    IReadOnlyList<string> barcodes = ScanBarcodes(pdfBytes, pageCount);

    return Task.FromResult(new ExtractionResult
    {
      Text = textBuilder.ToString().Trim(),
      Metadata = metadata,
      PageCount = Math.Max(pageCount, 1),
      Barcodes = barcodes,
    });
  }

  public async Task<ExtractionResult> ExtractFromOriginalAsync(
    byte[] content,
    string contentType,
    string fileName,
    CancellationToken cancellationToken)
  {
    DocumentFormatKind kind = _formatRouter.Classify(contentType);

    if (kind is DocumentFormatKind.Image)
    {
      return new ExtractionResult
      {
        PageCount = 1,
        Metadata = await SafeTikaMetadataAsync(content, contentType, cancellationToken),
      };
    }

    Dictionary<string, string> metadata = await SafeTikaMetadataAsync(content, contentType, cancellationToken);
    string text = await SafeTikaTextAsync(content, contentType, cancellationToken);

    return new ExtractionResult
    {
      Text = text,
      Metadata = metadata,
      PageCount = 1,
    };
  }

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

  private IReadOnlyList<string> ScanBarcodes(byte[] pdfBytes, int pageCount)
  {
    HashSet<string> values = new(StringComparer.Ordinal);
    int pagesToScan = Math.Min(pageCount, _renderingOptions.MaxBarcodeScanPages);
    BarcodeReader reader = new()
    {
      AutoRotate = true,
      Options = new ZXing.Common.DecodingOptions
      {
        TryHarder = true,
        PossibleFormats =
        [
          BarcodeFormat.QR_CODE,
          BarcodeFormat.CODE_128,
          BarcodeFormat.CODE_39,
          BarcodeFormat.EAN_13,
          BarcodeFormat.EAN_8,
          BarcodeFormat.DATA_MATRIX,
          BarcodeFormat.PDF_417,
        ],
      },
    };

    for (int pageIndex = 0; pageIndex < pagesToScan; pageIndex++)
    {
      try
      {
        using SKBitmap bitmap = Conversion.ToImage(
          pdfBytes,
          (Index)pageIndex,
          options: new(Dpi: 120));

        Result? result = reader.Decode(bitmap);
        if (result is not null && !string.IsNullOrWhiteSpace(result.Text))
        {
          values.Add(result.Text);
        }
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Barcode scan failed on page {PageNumber}.", pageIndex + 1);
      }
    }

    return values.ToList();
  }

  private static void AddIfPresent(Dictionary<string, string> metadata, string key, string? value)
  {
    if (!string.IsNullOrWhiteSpace(value))
    {
      metadata[key] = value;
    }
  }
}
