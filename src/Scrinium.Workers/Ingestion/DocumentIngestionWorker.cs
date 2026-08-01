using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Extraction;
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using Scrinium.Core.Storage;
using Scrinium.Core.Telemetry;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

public sealed class DocumentIngestionWorker : BackgroundService
{
  private const string ConsumerGroup = "document-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<DocumentIngestionWorker> _logger;

  public DocumentIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<DocumentIngestionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _jobQueue = jobQueue;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await _jobQueue.EnsureConsumerGroupAsync(
      IngestStreamName.Documents,
      ConsumerGroup,
      stoppingToken);

    _logger.LogInformation("Document ingestion worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<DocumentIngestJob>? message = await _jobQueue.DequeueAsync<DocumentIngestJob>(
        IngestStreamName.Documents,
        ConsumerGroup,
        _options.ConsumerName,
        stoppingToken);

      if (message is null)
      {
        await Task.Delay(_options.PollIntervalMs, stoppingToken);
        continue;
      }

      try
      {
        await ProcessAsync(message.Payload, stoppingToken);
        await _jobQueue.AcknowledgeAsync(
          IngestStreamName.Documents,
          ConsumerGroup,
          message.MessageId,
          stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(
          ex,
          "Document job failed for {DocumentId}. Message will remain pending for reclaim.",
          message.Payload.DocumentId);

        await MarkDocumentErrorAsync(message.Payload.DocumentId, ex.Message, stoppingToken);
      }
    }
  }

  private async Task ProcessAsync(DocumentIngestJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    IDocumentRepository documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    IBlobStore blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
    IDocumentNormalizer normalizer = scope.ServiceProvider.GetRequiredService<IDocumentNormalizer>();
    IDocumentExtractor extractor = scope.ServiceProvider.GetRequiredService<IDocumentExtractor>();
    IFormatRouter formatRouter = scope.ServiceProvider.GetRequiredService<IFormatRouter>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    Document? document = await documents.GetByIdAsync(job.DocumentId, cancellationToken);
    if (document is null)
    {
      throw new InvalidOperationException($"Document {job.DocumentId} was not found.");
    }

    byte[] originalBytes = await File.ReadAllBytesAsync(job.StagingPath, cancellationToken);
    DocumentFormatKind formatKind = formatRouter.Classify(job.ContentType);

    using (IIngestStep storeStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "store_original",
      "document",
      _options.ConsumerName,
      job.TraceId)))
    {
      string objectKey = BlobKeys.Original(job.DocumentId, job.FileName);
      await using MemoryStream stagingStream = new(originalBytes);
      await blobStore.PutAsync(objectKey, stagingStream, cancellationToken);
      document.Status = DocumentStatus.Extracting;
      document.ProcessingStep = "store_original";
      document.IngestStartedAt ??= DateTimeOffset.UtcNow;
      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);
      storeStep.CompleteSuccess();
    }

    NormalizeResult normalizeResult;
    using (IIngestStep normalizeStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "normalize_pdf",
      "document",
      _options.ConsumerName,
      job.TraceId)))
    {
      document.ProcessingStep = "normalize_pdf";
      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);

      normalizeResult = await normalizer.NormalizeAsync(
        originalBytes,
        job.ContentType,
        job.FileName,
        cancellationToken);

      if (normalizeResult.PdfBytes.Length > 0)
      {
        string pdfKey = BlobKeys.NormalizedPdf(job.DocumentId);
        await using MemoryStream pdfStream = new(normalizeResult.PdfBytes);
        await blobStore.PutAsync(pdfKey, pdfStream, cancellationToken);
      }

      normalizeStep.CompleteSuccess();
    }

    ExtractionResult extraction;
    using (IIngestStep extractStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "extract_content",
      "document",
      _options.ConsumerName,
      job.TraceId)))
    {
      document.ProcessingStep = "extract_content";
      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);

      if (normalizeResult.PdfBytes.Length > 0)
      {
        extraction = await extractor.ExtractFromPdfAsync(normalizeResult.PdfBytes, cancellationToken);
        ExtractionResult tikaMetadata = await extractor.ExtractFromOriginalAsync(
          originalBytes,
          job.ContentType,
          job.FileName,
          cancellationToken);

        foreach (KeyValuePair<string, string> entry in tikaMetadata.Metadata)
        {
          extraction.Metadata.TryAdd(entry.Key, entry.Value);
        }

        if (string.IsNullOrWhiteSpace(extraction.Text) && !string.IsNullOrWhiteSpace(tikaMetadata.Text))
        {
          extraction.Text = tikaMetadata.Text;
        }
      }
      else
      {
        extraction = await extractor.ExtractFromOriginalAsync(
          originalBytes,
          job.ContentType,
          job.FileName,
          cancellationToken);
      }

      document.ExtractedText = extraction.Text;
      document.ExtractedMetadata = extraction.Metadata;
      document.ExtractionWarnings = extraction.Warnings;
      document.PageCount = Math.Max(extraction.PageCount, 1);

      if (extraction.Barcodes.Count > 0)
      {
        document.ExtractedMetadata["barcodes"] = string.Join(", ", extraction.Barcodes);
      }

      extractStep.CompleteSuccess();
    }

    using (IIngestStep fanOutStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "fan_out_pages",
      "document",
      _options.ConsumerName,
      job.TraceId)))
    {
      document.Status = DocumentStatus.Rendering;
      document.ProcessingStep = "fan_out_pages";

      HashSet<int> existingPages = document.Pages.Select(x => x.PageNumber).ToHashSet();
      for (int pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
      {
        if (existingPages.Contains(pageNumber))
        {
          continue;
        }

        document.Pages.Add(new DocumentPage
        {
          DocumentId = document.Id,
          PageNumber = pageNumber,
          SourceKind = formatKind is DocumentFormatKind.Image
            ? PageSourceKind.Image
            : PageSourceKind.PdfPage,
          ProcessingStatus = PageProcessingStatus.Pending,
          RenderStatus = PageProcessingStatus.Pending,
        });
      }

      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);

      for (int pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
      {
        await _jobQueue.EnqueueAsync(
          IngestStreamName.Pages,
          new PageIngestJob
          {
            DocumentId = document.Id,
            PageNumber = pageNumber,
            TraceId = job.TraceId,
          },
          cancellationToken);
      }

      fanOutStep.CompleteSuccess();
    }

    if (File.Exists(job.StagingPath))
    {
      File.Delete(job.StagingPath);
    }
  }

  private async Task MarkDocumentErrorAsync(
    Guid documentId,
    string error,
    CancellationToken cancellationToken)
  {
    try
    {
      using IServiceScope scope = _scopeFactory.CreateScope();
      IDocumentRepository documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
      Document? document = await documents.GetByIdAsync(documentId, cancellationToken);
      if (document is null)
      {
        return;
      }

      document.Status = DocumentStatus.Error;
      document.LastError = error;
      document.ProcessingStep = "error";
      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to mark document {DocumentId} as error.", documentId);
    }
  }
}
