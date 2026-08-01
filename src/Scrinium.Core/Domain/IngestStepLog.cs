using System;
using System.Collections.Generic;

namespace Scrinium.Core.Domain;

public class IngestStepLog
{
  public Guid Id { get; set; }

  public Guid? ArchiveId { get; set; }

  public Guid? ArchiveSheetId { get; set; }

  public Guid? BundleId { get; set; }

  public string StepName { get; set; } = string.Empty;

  public string WorkerType { get; set; } = string.Empty;

  public string WorkerId { get; set; } = string.Empty;

  public DateTimeOffset StartedAt { get; set; }

  public DateTimeOffset? CompletedAt { get; set; }

  public long? DurationMs { get; set; }

  public string Status { get; set; } = string.Empty;

  public string? ErrorMessage { get; set; }

  public string? TraceId { get; set; }

  public Dictionary<string, object?> Metadata { get; set; } = new();
}
