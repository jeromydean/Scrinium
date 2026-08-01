using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
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

  public Task<IRasterizedPage> RasterizePdfPageAsync(
    byte[] pdfBytes,
    int pageNumber,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    int pageIndex = pageNumber - 1;
    if (pageIndex < 0 || pageIndex >= Conversion.GetPageCount(pdfBytes))
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), "PDF page number is out of range.");
    }

    SKBitmap sourceBitmap = Conversion.ToImage(
      pdfBytes,
      (Index)pageIndex,
      options: new(Dpi: _options.PdfRenderDpi));

    return Task.FromResult<IRasterizedPage>(new SkiaRasterizedPage(sourceBitmap));
  }

  public Task<IRasterizedPage> DecodeImageAsync(
    byte[] imageBytes,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    SKBitmap sourceBitmap = SKBitmap.Decode(imageBytes)
      ?? throw new InvalidOperationException("Unable to decode image for rendering.");

    return Task.FromResult<IRasterizedPage>(new SkiaRasterizedPage(sourceBitmap));
  }

  public async Task<IReadOnlyDictionary<RenderTier, string>> UploadRenderTiersAsync(
    IRasterizedPage page,
    Guid archiveId,
    Guid archiveSheetId,
    int sequenceInArchive,
    CancellationToken cancellationToken)
  {
    if (page is not SkiaRasterizedPage skiaPage)
    {
      throw new ArgumentException("Unsupported rasterized page implementation.", nameof(page));
    }

    Dictionary<RenderTier, string> objectKeys = new();
    foreach (RenderTier tier in Enum.GetValues<RenderTier>())
    {
      cancellationToken.ThrowIfCancellationRequested();

      string objectKey = BlobKeys.SheetRender(archiveId, archiveSheetId, tier, sequenceInArchive);
      await using MemoryStream webpStream = EncodeTier(skiaPage.Bitmap, tier);
      await _blobStore.PutAsync(objectKey, webpStream, cancellationToken);
      objectKeys[tier] = objectKey;
    }

    return objectKeys;
  }

  public string ExtractPdfPageText(byte[] pdfBytes, int pageNumber)
  {
    using PdfDocument document = PdfDocument.Open(pdfBytes);
    Page page = document.GetPage(pageNumber);
    return page.Text?.Trim() ?? string.Empty;
  }

  private MemoryStream EncodeTier(SKBitmap sourceBitmap, RenderTier tier)
  {
    int targetWidth = RenderTierDefaults.GetTargetWidth(tier);
    float scale = targetWidth / (float)sourceBitmap.Width;
    int targetHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));

    SKImageInfo info = new(targetWidth, targetHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);
    using SKBitmap resized = sourceBitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
      ?? throw new InvalidOperationException("Failed to resize page bitmap.");

    using SKImage image = SKImage.FromBitmap(resized);
    using SKData data = image.Encode(SKEncodedImageFormat.Webp, _options.WebpQuality);
    MemoryStream stream = new();
    data.AsStream().CopyTo(stream);
    stream.Position = 0;
    return stream;
  }
}
