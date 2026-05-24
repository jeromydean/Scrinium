using Scrinium.Api.Models;
using Scrinium.Api.Services;

namespace Scrinium.Api.Workers
{
  public sealed class IngestionBackgroundService : BackgroundService
  {
    private readonly IIngestionQueue _ingestionQueue;
    private readonly ILogger<IngestionBackgroundService> _logger;

    public IngestionBackgroundService(
      IIngestionQueue ingestionQueue,
      ILogger<IngestionBackgroundService> logger)
    {
      _ingestionQueue = ingestionQueue;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("Ingestion background worker started.");

      while (!stoppingToken.IsCancellationRequested)
      {
        IngestionJob job = await _ingestionQueue.DequeueAsync(stoppingToken);

        try
        {
          await ProcessJobAsync(job, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          throw;
        }
        catch (Exception ex)
        {
          _logger.LogError(
            ex,
            "Ingestion failed for document {DocumentId} ({FileName}).",
            job.DocumentId,
            job.FileName);
        }
      }
    }

    private Task ProcessJobAsync(IngestionJob job, CancellationToken cancellationToken)
    {
      // Phase 1/2 pipeline (Tika, Gotenberg, MinIO, Postgres, Solr) will run here.
      _logger.LogInformation(
        "Processing ingestion job {DocumentId}: {FileName} ({FileSizeBytes} bytes) at {StagingPath}.",
        job.DocumentId,
        job.FileName,
        job.FileSizeBytes,
        job.StagingPath);

      return Task.CompletedTask;
    }
  }
}
