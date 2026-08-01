using System;
using System.Collections.Generic;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class Bundle
{
  public Guid Id { get; set; }

  public string Title { get; set; } = string.Empty;

  public BundleStatus Status { get; set; } = BundleStatus.Processing;

  public bool IsDefault { get; set; }

  public Guid? SourceArchiveId { get; set; }

  public int SheetsFailedCount { get; set; }

  public IngestQuality? IngestQuality { get; set; }

  public Guid CreatedBy { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? ReadyAt { get; set; }

  public bool FinalizeEnqueued { get; set; }

  public string? TraceId { get; set; }

  public Archive? SourceArchive { get; set; }

  public ICollection<Sheet> Sheets { get; set; } = new List<Sheet>();

  public ICollection<BundleTag> BundleTags { get; set; } = new List<BundleTag>();
}
