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

public sealed class FinalizeBundleWorker : BackgroundService
{
  private const string ConsumerGroup = "finalize-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<FinalizeBundleWorker> _logger;

  public FinalizeBundleWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<FinalizeBundleWorker> logger)
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

    _logger.LogInformation("Finalize bundle worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<FinalizeBundleJob>? message = await _jobQueue.DequeueAsync<FinalizeBundleJob>(
        IngestStreamName.Finalize,
        ConsumerGroup,
        _options.ConsumerName,
        stoppingToken);

      if (message is null)
      {
        await Task.Delay(_options.PollIntervalMs, stoppingToken);
        continue;
      }

      await IngestionJobRunner.RunAsync(
        message,
        IngestStreamName.Finalize,
        ConsumerGroup,
        _jobQueue,
        _options,
        _logger,
        ProcessAsync,
        giveUpAsync: null,
        stoppingToken);
    }
  }

  private async Task ProcessAsync(FinalizeBundleJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
    IArchiveRepository archives = scope.ServiceProvider.GetRequiredService<IArchiveRepository>();
    IBundleRepository bundles = scope.ServiceProvider.GetRequiredService<IBundleRepository>();
    ISearchIndexer searchIndexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();
    IBundleReadyNotifier notifier = scope.ServiceProvider.GetRequiredService<IBundleReadyNotifier>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    Bundle? bundle = await bundles.GetByIdAsync(job.BundleId, cancellationToken);
    if (bundle is null)
    {
      throw new InvalidOperationException($"Bundle {job.BundleId} was not found.");
    }

    if (bundle.Status is BundleStatus.Ready)
    {
      _logger.LogInformation("Bundle {BundleId} is already ready; skipping finalize replay.", job.BundleId);
      return;
    }

    int failedSheets = await dbContext.Sheets
      .Where(x => x.BundleId == job.BundleId)
      .CountAsync(
        x => x.ArchiveSheet.RenderStatus == PageProcessingStatus.Error
          || x.ArchiveSheet.ProcessingStatus == PageProcessingStatus.Error,
        cancellationToken);

    bundle.SheetsFailedCount = failedSheets;
    bundle.IngestQuality = failedSheets == 0 ? IngestQuality.Complete : IngestQuality.Degraded;

    Archive? archive = await archives.GetByIdAsync(job.ArchiveId, cancellationToken);
    if (archive is not null)
    {
      archive.SheetsFailedCount = failedSheets;
      archive.IngestQuality = bundle.IngestQuality;
      archive.Status = ArchiveStatus.Ready;
      archive.IngestCompletedAt = DateTimeOffset.UtcNow;
      archive.ProcessingStep = "ready";
      await archives.UpdateAsync(archive, cancellationToken);
      await archives.SaveChangesAsync(cancellationToken);
    }

    using (IIngestStep indexStep = telemetry.BeginStep(new IngestStepContext(
      job.ArchiveId,
      null,
      job.BundleId,
      BundleIngestSteps.IndexSolr,
      "finalize",
      _options.ConsumerName,
      job.TraceId)))
    {
      try
      {
        await searchIndexer.IndexBundleAsync(job.BundleId, cancellationToken);
        indexStep.CompleteSuccess();
      }
      catch (Exception ex)
      {
        indexStep.CompleteError(ex.Message);
        _logger.LogError(ex, "Solr indexing failed for bundle {BundleId}.", job.BundleId);
      }
    }

    using (IIngestStep finalizeStep = telemetry.BeginStep(new IngestStepContext(
      job.ArchiveId,
      null,
      job.BundleId,
      BundleIngestSteps.Finalize,
      "finalize",
      _options.ConsumerName,
      job.TraceId)))
    {
      bundle.Status = BundleStatus.Ready;
      bundle.ReadyAt = DateTimeOffset.UtcNow;
      await bundles.UpdateAsync(bundle, cancellationToken);
      await bundles.SaveChangesAsync(cancellationToken);
      finalizeStep.CompleteSuccess();
    }

    await notifier.NotifyBundleReadyAsync(job.BundleId, cancellationToken);
  }
}
