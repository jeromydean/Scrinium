using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scrinium.Core.Domain;
using Scrinium.Core.Ports;
using Scrinium.Core.Telemetry;
using Scrinium.Infrastructure.Persistence;

namespace Scrinium.Infrastructure.Telemetry;

public sealed class PostgresIngestTelemetry : IIngestTelemetry
{
  private readonly ScriniumDbContext _dbContext;
  private readonly ILogger<PostgresIngestTelemetry> _logger;

  public PostgresIngestTelemetry(
    ScriniumDbContext dbContext,
    ILogger<PostgresIngestTelemetry> logger)
  {
    _dbContext = dbContext;
    _logger = logger;
  }

  public IIngestStep BeginStep(IngestStepContext context)
  {
    return new PostgresIngestStep(_dbContext, _logger, context);
  }

  private sealed class PostgresIngestStep : IIngestStep
  {
    private readonly ScriniumDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly IngestStepContext _context;
    private readonly Stopwatch _stopwatch;
    private readonly IngestStepLog _logEntry;
    private bool _completed;

    public PostgresIngestStep(
      ScriniumDbContext dbContext,
      ILogger logger,
      IngestStepContext context)
    {
      _dbContext = dbContext;
      _logger = logger;
      _context = context;
      _stopwatch = Stopwatch.StartNew();
      _logEntry = new IngestStepLog
      {
        Id = Guid.NewGuid(),
        DocumentId = context.DocumentId,
        PageNumber = context.PageNumber,
        StepName = context.StepName,
        WorkerType = context.WorkerType,
        WorkerId = context.WorkerId,
        StartedAt = DateTimeOffset.UtcNow,
        Status = "running",
        TraceId = context.TraceId,
      };

      _dbContext.IngestStepLogs.Add(_logEntry);
      _ = _dbContext.SaveChangesAsync();

      using (_logger.BeginScope(new
      {
        _context.DocumentId,
        _context.PageNumber,
        _context.StepName,
        _context.WorkerType,
        _context.TraceId,
      }))
      {
        _logger.LogInformation("Ingest step {StepName} started.", _context.StepName);
      }
    }

    public void CompleteSuccess()
    {
      Complete("success", null);
    }

    public void CompleteError(string message)
    {
      Complete("error", message);
    }

    public void RecordRetry(int attempt)
    {
      _logEntry.Status = "retry";
      _logEntry.Metadata["attempt"] = attempt;
      _ = _dbContext.SaveChangesAsync();

      _logger.LogWarning(
        "Ingest step {StepName} retry attempt {Attempt} for document {DocumentId}.",
        _context.StepName,
        attempt,
        _context.DocumentId);
    }

    public void Dispose()
    {
      if (!_completed)
      {
        CompleteError("Step disposed without explicit completion.");
      }
    }

    private void Complete(string status, string? errorMessage)
    {
      if (_completed)
      {
        return;
      }

      _completed = true;
      _stopwatch.Stop();
      _logEntry.Status = status;
      _logEntry.ErrorMessage = errorMessage;
      _logEntry.CompletedAt = DateTimeOffset.UtcNow;
      _logEntry.DurationMs = _stopwatch.ElapsedMilliseconds;
      _ = _dbContext.SaveChangesAsync();

      using (_logger.BeginScope(new
      {
        _context.DocumentId,
        _context.PageNumber,
        _context.StepName,
        DurationMs = _logEntry.DurationMs,
        _context.TraceId,
      }))
      {
        if (status == "success")
        {
          _logger.LogInformation(
            "Ingest step {StepName} completed in {DurationMs} ms.",
            _context.StepName,
            _logEntry.DurationMs);
        }
        else
        {
          _logger.LogError(
            "Ingest step {StepName} failed in {DurationMs} ms: {Error}.",
            _context.StepName,
            _logEntry.DurationMs,
            errorMessage);
        }
      }
    }
  }
}
