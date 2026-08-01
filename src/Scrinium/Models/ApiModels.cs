using System;
using System.Collections.Generic;

namespace Scrinium.Models;

public sealed class BundleListResponse
{
  public BundleSummaryResponse[] Items { get; set; } = [];

  public int TotalCount { get; set; }

  public int Skip { get; set; }

  public int Take { get; set; }
}

public sealed class BundleSummaryResponse
{
  public Guid BundleId { get; set; }

  public Guid? ArchiveId { get; set; }

  public string Title { get; set; } = string.Empty;

  public string Status { get; set; } = string.Empty;

  public string? IngestQuality { get; set; }

  public int SheetCount { get; set; }

  public int SheetsFailedCount { get; set; }

  public bool IsDefault { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? ReadyAt { get; set; }

  public string[] Tags { get; set; } = [];
}

public sealed class BundleDetailResponse
{
  public Guid BundleId { get; set; }

  public Guid? ArchiveId { get; set; }

  public string Title { get; set; } = string.Empty;

  public string Status { get; set; } = string.Empty;

  public string? IngestQuality { get; set; }

  public int SheetCount { get; set; }

  public int SheetsFailedCount { get; set; }

  public bool IsDefault { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? ReadyAt { get; set; }

  public string[] Tags { get; set; } = [];

  public BundleSheetResponse[] Sheets { get; set; } = [];
}

public sealed class BundleSheetResponse
{
  public Guid ArchiveSheetId { get; set; }

  public int SortOrder { get; set; }

  public int SequenceInArchive { get; set; }

  public string ProcessingStatus { get; set; } = string.Empty;

  public string RenderStatus { get; set; } = string.Empty;

  public bool HasTextLayer { get; set; }
}
