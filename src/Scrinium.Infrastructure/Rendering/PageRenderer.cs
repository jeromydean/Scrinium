using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ports;
using Scrinium.Core.Rendering;
using Scrinium.Core.Storage;
using Scrinium.Infrastructure.Options;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Scrinium.Infrastructure.Rendering;

public sealed class PageRenderer : IPageRenderer
{
  private readonly IBlobStore _blobStore;
  private readonly RenderingOptions _options;

  public PageRenderer(
    IBlobStore blobStore,
    IOptions<RenderingOptions> options,
    ILogger<PageRenderer> logger)
  {
    _blobStore = blobStore;
    _options = options.Value;
    _ = logger;
  }

  public async Task<PageRenderResult> RenderPdfPageAsync(
    byte[] pdfBytes,
    int pageNumber,
    Guid documentId,
    CancellationToken cancellationToken)
  {
    int pageIndex = pageNumber - 1;
    if (pageIndex < 0 || pageIndex >= Conversion.GetPageCount(pdfBytes))
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), "PDF page number is out of range.");
    }

    using SKBitmap sourceBitmap = Conversion.ToImage(
      pdfBytes,
      (Index)pageIndex,
      options: new(Dpi: _options.PdfRenderDpi));

    PageRenderResult result = await UploadTiersAsync(sourceBitmap, documentId, pageNumber, cancellationToken);
    result.PlainText = ExtractPageText(pdfBytes, pageNumber);
    result.HasTextLayer = !string.IsNullOrWhiteSpace(result.PlainText);
    return result;
  }

  public async Task<PageRenderResult> RenderImageAsync(
    byte[] imageBytes,
    string contentType,
    int pageNumber,
    Guid documentId,
    CancellationToken cancellationToken)
  {
    using SKBitmap sourceBitmap = SKBitmap.Decode(imageBytes)
      ?? throw new InvalidOperationException("Unable to decode image for rendering.");

    return await UploadTiersAsync(sourceBitmap, documentId, pageNumber, cancellationToken);
  }

  private async Task<PageRenderResult> UploadTiersAsync(
    SKBitmap sourceBitmap,
    Guid documentId,
    int pageNumber,
    CancellationToken cancellationToken)
  {
    PageRenderResult result = new();
    foreach (RenderTier tier in Enum.GetValues<RenderTier>())
    {
      string objectKey = BlobKeys.PageRender(documentId, tier, pageNumber);
      await using MemoryStream webpStream = EncodeTier(sourceBitmap, tier);
      await _blobStore.PutAsync(objectKey, webpStream, cancellationToken);
      result.ObjectKeys[tier] = objectKey;
    }

    return result;
  }

  private MemoryStream EncodeTier(SKBitmap sourceBitmap, RenderTier tier)
  {
    int targetWidth = RenderTierDefaults.GetTargetWidth(tier);
    float scale = targetWidth / (float)sourceBitmap.Width;
    int targetHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));

    SKImageInfo info = new(targetWidth, targetHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);
    using SKBitmap resized = sourceBitmap.Resize(info, SKFilterQuality.High)
      ?? throw new InvalidOperationException("Failed to resize page bitmap.");

    using SKImage image = SKImage.FromBitmap(resized);
    using SKData data = image.Encode(SKEncodedImageFormat.Webp, _options.WebpQuality);
    MemoryStream stream = new();
    data.AsStream().CopyTo(stream);
    stream.Position = 0;
    return stream;
  }

  private static string ExtractPageText(byte[] pdfBytes, int pageNumber)
  {
    using PdfDocument document = PdfDocument.Open(pdfBytes);
    Page page = document.GetPage(pageNumber);
    return page.Text?.Trim() ?? string.Empty;
  }
}
