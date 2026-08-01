using System;
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
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using Scrinium.Core.Telemetry;
using Scrinium.Infrastructure.Persistence;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

public sealed class FinalizeIngestionWorker : BackgroundService
{
  private const string ConsumerGroup = "finalize-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<FinalizeIngestionWorker> _logger;

  public FinalizeIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<FinalizeIngestionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _jobQueue = jobQueue;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await _jobQueue.EnsureConsumerGroupAsync(
      IngestStreamName.Finalize,
      ConsumerGroup,
      stoppingToken);

    _logger.LogInformation("Finalize ingestion worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<FinalizeIngestJob>? message = await _jobQueue.DequeueAsync<FinalizeIngestJob>(
        IngestStreamName.Finalize,
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
          IngestStreamName.Finalize,
          ConsumerGroup,
          message.MessageId,
          stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Finalize job failed for document {DocumentId}.", message.Payload.DocumentId);
      }
    }
  }

  private async Task ProcessAsync(FinalizeIngestJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
    IDocumentRepository documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    ISearchIndexer searchIndexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();
    IDocumentReadyNotifier notifier = scope.ServiceProvider.GetRequiredService<IDocumentReadyNotifier>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    Document? document = await documents.GetByIdAsync(job.DocumentId, cancellationToken);
    if (document is null)
    {
      throw new InvalidOperationException($"Document {job.DocumentId} was not found.");
    }

    int failedPages = await dbContext.DocumentPages.CountAsync(
      x => x.DocumentId == job.DocumentId && x.RenderStatus == PageProcessingStatus.Error,
      cancellationToken);

    document.PagesFailedCount = failedPages;
    document.IngestQuality = failedPages == 0 ? IngestQuality.Complete : IngestQuality.Degraded;
    document.Status = DocumentStatus.Indexing;
    document.ProcessingStep = "indexing";
    await documents.UpdateAsync(document, cancellationToken);
    await documents.SaveChangesAsync(cancellationToken);

    using (IIngestStep indexStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "index_solr",
      "finalize",
      _options.ConsumerName,
      job.TraceId)))
    {
      try
      {
        await searchIndexer.IndexDocumentAsync(job.DocumentId, cancellationToken);
        indexStep.CompleteSuccess();
      }
      catch (Exception ex)
      {
        indexStep.CompleteError(ex.Message);
        _logger.LogError(ex, "Solr indexing failed for document {DocumentId}.", job.DocumentId);
        document.ExtractionWarnings["solr_index_error"] = ex.Message;
      }
    }

    using (IIngestStep finalizeStep = telemetry.BeginStep(new IngestStepContext(
      job.DocumentId,
      null,
      "finalize",
      "finalize",
      _options.ConsumerName,
      job.TraceId)))
    {
      document.Status = DocumentStatus.Ready;
      document.ProcessingStep = "ready";
      document.ReadyAt = DateTimeOffset.UtcNow;
      document.IngestCompletedAt = DateTimeOffset.UtcNow;
      await documents.UpdateAsync(document, cancellationToken);
      await documents.SaveChangesAsync(cancellationToken);
      finalizeStep.CompleteSuccess();
    }

    try
    {
      await notifier.NotifyDocumentReadyAsync(job.DocumentId, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to notify clients that document {DocumentId} is ready.", job.DocumentId);
    }

    _logger.LogInformation(
      "Document {DocumentId} is ready with quality {IngestQuality} ({PagesFailedCount} failed pages).",
      document.Id,
      document.IngestQuality,
      document.PagesFailedCount);
  }
}
