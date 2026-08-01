using System;
using System.Collections.Generic;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class Archive
{
  public Guid Id { get; set; }

  public ArchiveStatus Status { get; set; } = ArchiveStatus.Uploading;

  public string? ProcessingStep { get; set; }

  public string OriginalFileName { get; set; } = string.Empty;

  public string ContentType { get; set; } = string.Empty;

  public long ByteSize { get; set; }

  public int SheetCount { get; set; }

  public int SheetsFailedCount { get; set; }

  public IngestQuality? IngestQuality { get; set; }

  public Dictionary<string, string> ExtractedMetadata { get; set; } = new();

  public Dictionary<string, string> ClientMetadata { get; set; } = new();

  public Dictionary<string, object?> ExtractionWarnings { get; set; } = new();

  public Guid UploadedBy { get; set; }

  public DateTimeOffset UploadedAt { get; set; }

  public DateTimeOffset? IngestStartedAt { get; set; }

  public DateTimeOffset? IngestCompletedAt { get; set; }

  public string? LastError { get; set; }

  public string? IdempotencyKey { get; set; }

  public string? TraceId { get; set; }

  public string? StagingPath { get; set; }

  public ICollection<ArchiveSheet> ArchiveSheets { get; set; } = new List<ArchiveSheet>();

  public ICollection<Bundle> Bundles { get; set; } = new List<Bundle>();
}
