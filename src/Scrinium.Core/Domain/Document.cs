using System;
using System.Collections.Generic;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class Document
{
  public Guid Id { get; set; }

  public DocumentStatus Status { get; set; } = DocumentStatus.Uploading;

  public string? ProcessingStep { get; set; }

  public string OriginalFileName { get; set; } = string.Empty;

  public string ContentType { get; set; } = string.Empty;

  public long ByteSize { get; set; }

  public int PageCount { get; set; }

  public int PagesFailedCount { get; set; }

  public IngestQuality? IngestQuality { get; set; }

  public string? ExtractedText { get; set; }

  public Dictionary<string, string> ExtractedMetadata { get; set; } = new();

  public Dictionary<string, string> ClientMetadata { get; set; } = new();

  public Dictionary<string, object?> ExtractionWarnings { get; set; } = new();

  public Guid UploadedBy { get; set; }

  public DateTimeOffset UploadedAt { get; set; }

  public DateTimeOffset? ReadyAt { get; set; }

  public DateTimeOffset? IngestStartedAt { get; set; }

  public DateTimeOffset? IngestCompletedAt { get; set; }

  public string? LastError { get; set; }

  public string? IdempotencyKey { get; set; }

  public bool FinalizeEnqueued { get; set; }

  public string? TraceId { get; set; }

  public string? StagingPath { get; set; }

  public ICollection<DocumentPage> Pages { get; set; } = new List<DocumentPage>();

  public ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();
}
