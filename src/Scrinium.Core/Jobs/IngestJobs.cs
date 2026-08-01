using System;
using System.Collections.Generic;

namespace Scrinium.Core.Jobs;

public class DocumentIngestJob
{
  public Guid DocumentId { get; set; }

  public string StagingPath { get; set; } = string.Empty;

  public string FileName { get; set; } = string.Empty;

  public string ContentType { get; set; } = string.Empty;

  public Guid UploadedBy { get; set; }

  public IReadOnlyList<string> InitialTags { get; set; } = Array.Empty<string>();

  public IReadOnlyDictionary<string, string> ClientMetadata { get; set; }
    = new Dictionary<string, string>();

  public string TraceId { get; set; } = string.Empty;
}

public class PageIngestJob
{
  public Guid DocumentId { get; set; }

  public int PageNumber { get; set; }

  public string TraceId { get; set; } = string.Empty;
}

public class FinalizeIngestJob
{
  public Guid DocumentId { get; set; }

  public string TraceId { get; set; } = string.Empty;
}

public class QueueMessage<T>
{
  public string MessageId { get; set; } = string.Empty;

  public T Payload { get; set; } = default!;
}
