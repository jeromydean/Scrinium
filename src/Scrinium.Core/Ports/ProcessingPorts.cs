using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Core.Extraction;
using Scrinium.Core.Rendering;

namespace Scrinium.Core.Ports;

public interface IFormatRouter
{
  DocumentFormatKind Classify(string contentType);
}

public interface IDocumentNormalizer
{
  Task<NormalizeResult> NormalizeAsync(
    byte[] content,
    string contentType,
    string fileName,
    CancellationToken cancellationToken);
}

public interface IDocumentExtractor
{
  Task<Dictionary<string, string>> ExtractPdfMetadataAsync(
    byte[] pdfBytes,
    CancellationToken cancellationToken);

  Task<Dictionary<string, string>> ExtractTikaMetadataAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken);

  Task<string> ExtractTikaTextAsync(
    byte[] content,
    string contentType,
    CancellationToken cancellationToken);

  int GetPdfPageCount(byte[] pdfBytes);
}

public interface IBarcodeScanner
{
  Task<IReadOnlyList<BarcodeResult>> ScanAsync(
    IRasterizedPage page,
    CancellationToken cancellationToken);
}

public interface IPageRenderer
{
  Task<IRasterizedPage> RasterizePdfPageAsync(
    byte[] pdfBytes,
    int pageNumber,
    CancellationToken cancellationToken);

  Task<IRasterizedPage> DecodeImageAsync(
    byte[] imageBytes,
    CancellationToken cancellationToken);

  Task<IReadOnlyDictionary<RenderTier, string>> UploadRenderTiersAsync(
    IRasterizedPage page,
    Guid archiveId,
    Guid archiveSheetId,
    int sequenceInArchive,
    CancellationToken cancellationToken);

  string ExtractPdfPageText(byte[] pdfBytes, int pageNumber);
}

public interface ISearchIndexer
{
  Task IndexBundleAsync(Guid bundleId, CancellationToken cancellationToken);
}

public interface IBundleReadyNotifier
{
  Task NotifyBundleReadyAsync(Guid bundleId, CancellationToken cancellationToken);
}
