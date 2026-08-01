using System;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Core.Extraction;

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
  Task<ExtractionResult> ExtractFromPdfAsync(
    byte[] pdfBytes,
    CancellationToken cancellationToken);

  Task<ExtractionResult> ExtractFromOriginalAsync(
    byte[] content,
    string contentType,
    string fileName,
    CancellationToken cancellationToken);
}

public interface IPageRenderer
{
  Task<PageRenderResult> RenderPdfPageAsync(
    byte[] pdfBytes,
    int pageNumber,
    Guid documentId,
    CancellationToken cancellationToken);

  Task<PageRenderResult> RenderImageAsync(
    byte[] imageBytes,
    string contentType,
    int pageNumber,
    Guid documentId,
    CancellationToken cancellationToken);
}

public interface ISearchIndexer
{
  Task IndexDocumentAsync(Guid documentId, CancellationToken cancellationToken);
}

public interface IDocumentReadyNotifier
{
  Task NotifyDocumentReadyAsync(Guid documentId, CancellationToken cancellationToken);
}
