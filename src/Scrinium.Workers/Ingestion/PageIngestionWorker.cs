using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
using Scrinium.Infrastructure.Persistence;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

public sealed class PageIngestionWorker : BackgroundService
{
  private const string ConsumerGroup = "page-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<PageIngestionWorker> _logger;

  public PageIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<PageIngestionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _jobQueue = jobQueue;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await _jobQueue.EnsureConsumerGroupAsync(
      IngestStreamName.Pages,
      ConsumerGroup,
      stoppingToken);

    _logger.LogInformation("Page ingestion worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<PageIngestJob>? message = await _jobQueue.DequeueAsync<PageIngestJob>(
        IngestStreamName.Pages,
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
          IngestStreamName.Pages,
          ConsumerGroup,
          message.MessageId,
          stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(
          ex,
          "Page job failed for document {DocumentId} page {PageNumber}.",
          message.Payload.DocumentId,
          message.Payload.PageNumber);
      }
    }
  }

  private async Task ProcessAsync(PageIngestJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
    IDocumentRepository documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    IBlobStore blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
    IPageRenderer pageRenderer = scope.ServiceProvider.GetRequiredService<IPageRenderer>();
    IFormatRouter formatRouter = scope.ServiceProvider.GetRequiredService<IFormatRouter>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    DocumentPage? page = await dbContext.DocumentPages
      .FirstOrDefaultAsync(
        x => x.DocumentId == job.DocumentId && x.PageNumber == job.PageNumber,
        cancellationToken);

    if (page is null)
    {
      throw new InvalidOperationException(
        $"Page {job.PageNumber} for document {job.DocumentId} was not found.");
    }

    if (page.RenderStatus is PageProcessingStatus.Ready or PageProcessingStatus.Error)
    {
      return;
    }

    Document? document = await documents.GetByIdAsync(job.DocumentId, cancellationToken);
    if (document is null)
    {
      throw new InvalidOperationException($"Document {job.DocumentId} was not found.");
    }

    using IIngestStep step = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      job.PageNumber,
      "render_page",
      "page",
      _options.ConsumerName,
      job.TraceId));

    try
    {
      string pdfKey = BlobKeys.NormalizedPdf(job.DocumentId);
      string originalKey = BlobKeys.Original(job.DocumentId, document.OriginalFileName);
      DocumentFormatKind formatKind = formatRouter.Classify(document.ContentType);

      PageRenderResult renderResult;
      if (formatKind is DocumentFormatKind.Image)
      {
        await using Stream imageStream = await blobStore.GetAsync(originalKey, cancellationToken);
        byte[] imageBytes = await ReadAllBytesAsync(imageStream, cancellationToken);
        renderResult = await pageRenderer.RenderImageAsync(
          imageBytes,
          document.ContentType,
          job.PageNumber,
          job.DocumentId,
          cancellationToken);
      }
      else
      {
        string sourceKey = await blobStore.ExistsAsync(pdfKey, cancellationToken)
          ? pdfKey
          : originalKey;

        await using Stream pdfStream = await blobStore.GetAsync(sourceKey, cancellationToken);
        byte[] pdfBytes = await ReadAllBytesAsync(pdfStream, cancellationToken);
        renderResult = await pageRenderer.RenderPdfPageAsync(
          pdfBytes,
          job.PageNumber,
          job.DocumentId,
          cancellationToken);
      }

      page.PlainText = renderResult.PlainText;
      page.HasTextLayer = renderResult.HasTextLayer;
      page.ProcessingStatus = PageProcessingStatus.Ready;
      page.RenderStatus = PageProcessingStatus.Ready;
      await dbContext.SaveChangesAsync(cancellationToken);
      step.CompleteSuccess();
    }
    catch (Exception ex)
    {
      page.ProcessingStatus = PageProcessingStatus.Error;
      page.RenderStatus = PageProcessingStatus.Error;
      page.LastError = ex.Message;
      await dbContext.SaveChangesAsync(cancellationToken);
      step.CompleteError(ex.Message);
      throw;
    }

    int pendingPages = await dbContext.DocumentPages.CountAsync(
      x => x.DocumentId == job.DocumentId
        && (x.RenderStatus == PageProcessingStatus.Pending
          || x.ProcessingStatus == PageProcessingStatus.Pending),
      cancellationToken);

    if (pendingPages > 0)
    {
      return;
    }

    if (await documents.TryMarkFinalizeEnqueuedAsync(job.DocumentId, cancellationToken))
    {
      await _jobQueue.EnqueueAsync(
        IngestStreamName.Finalize,
        new FinalizeIngestJob
        {
          DocumentId = job.DocumentId,
          TraceId = job.TraceId,
        },
        cancellationToken);
    }
  }

  private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
  {
    using MemoryStream memoryStream = new();
    await stream.CopyToAsync(memoryStream, cancellationToken);
    return memoryStream.ToArray();
  }
}
