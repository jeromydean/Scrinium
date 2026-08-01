using System;
using System.Collections.Generic;

namespace Scrinium.Api.Models;

public sealed class IngestionAcceptedResponse
{
  public Guid ArchiveId { get; set; }

  public string Status { get; set; } = "queued";

  public string FileName { get; set; } = string.Empty;

  public DateTimeOffset EnqueuedAt { get; set; }
}

public sealed class BundleListResponse
{
  public IReadOnlyList<BundleSummaryResponse> Items { get; set; } = Array.Empty<BundleSummaryResponse>();

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

  public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
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

  public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

  public IReadOnlyList<BundleSheetResponse> Sheets { get; set; } = Array.Empty<BundleSheetResponse>();
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
