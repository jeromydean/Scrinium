using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scrinium.IngestionClient.Api;

public sealed class IngestionAcceptedResponse
{
  [JsonPropertyName("archiveId")]
  public Guid ArchiveId { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; } = string.Empty;

  [JsonPropertyName("fileName")]
  public string FileName { get; set; } = string.Empty;

  [JsonPropertyName("enqueuedAt")]
  public DateTimeOffset EnqueuedAt { get; set; }
}

public sealed class BundleStatusResponse
{
  [JsonPropertyName("bundleId")]
  public Guid BundleId { get; set; }

  [JsonPropertyName("archiveId")]
  public Guid? ArchiveId { get; set; }

  [JsonPropertyName("title")]
  public string Title { get; set; } = string.Empty;

  [JsonPropertyName("status")]
  public string Status { get; set; } = string.Empty;

  [JsonPropertyName("ingestQuality")]
  public string? IngestQuality { get; set; }

  [JsonPropertyName("sheetCount")]
  public int SheetCount { get; set; }

  [JsonPropertyName("sheetsFailedCount")]
  public int SheetsFailedCount { get; set; }

  [JsonPropertyName("isDefault")]
  public bool IsDefault { get; set; }

  [JsonPropertyName("createdAt")]
  public DateTimeOffset CreatedAt { get; set; }

  [JsonPropertyName("readyAt")]
  public DateTimeOffset? ReadyAt { get; set; }

  [JsonPropertyName("tags")]
  public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

  [JsonPropertyName("sheets")]
  public IReadOnlyList<BundleSheetResponse> Sheets { get; set; } = Array.Empty<BundleSheetResponse>();
}

public sealed class BundleSheetResponse
{
  [JsonPropertyName("archiveSheetId")]
  public Guid ArchiveSheetId { get; set; }

  [JsonPropertyName("sortOrder")]
  public int SortOrder { get; set; }

  [JsonPropertyName("sequenceInArchive")]
  public int SequenceInArchive { get; set; }

  [JsonPropertyName("processingStatus")]
  public string ProcessingStatus { get; set; } = string.Empty;

  [JsonPropertyName("renderStatus")]
  public string RenderStatus { get; set; } = string.Empty;

  [JsonPropertyName("hasTextLayer")]
  public bool HasTextLayer { get; set; }
}

public sealed class TokenResponse
{
  [JsonPropertyName("access_token")]
  public string AccessToken { get; set; } = string.Empty;

  [JsonPropertyName("expires_in")]
  public int ExpiresIn { get; set; }
}

public sealed class ApiErrorResponse
{
  [JsonPropertyName("error")]
  public string? Error { get; set; }

  [JsonPropertyName("title")]
  public string? Title { get; set; }
}
