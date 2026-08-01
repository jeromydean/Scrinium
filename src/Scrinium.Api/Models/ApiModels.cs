using System;
using System.Collections.Generic;

namespace Scrinium.Api.Models;

public sealed class IngestionAcceptedResponse
{
  public Guid DocumentId { get; set; }

  public string Status { get; set; } = "queued";

  public string FileName { get; set; } = string.Empty;

  public DateTimeOffset EnqueuedAt { get; set; }
}

public sealed class DocumentStatusResponse
{
  public Guid DocumentId { get; set; }

  public string Status { get; set; } = string.Empty;

  public string? IngestQuality { get; set; }

  public int PageCount { get; set; }

  public int PagesFailedCount { get; set; }

  public string? ProcessingStep { get; set; }

  public DateTimeOffset UploadedAt { get; set; }

  public DateTimeOffset? ReadyAt { get; set; }

  public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

  public IReadOnlyDictionary<string, string> ClientMetadata { get; set; }
    = new Dictionary<string, string>();
}
