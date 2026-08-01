using System;
using System.Collections.Generic;

namespace Scrinium.Core.Jobs;

public class ArchiveIngestJob
{
  public Guid ArchiveId { get; set; }

  public string StagingPath { get; set; } = string.Empty;

  public string FileName { get; set; } = string.Empty;

  public string ContentType { get; set; } = string.Empty;

  public Guid UploadedBy { get; set; }

  public IReadOnlyList<string> InitialTags { get; set; } = Array.Empty<string>();

  public IReadOnlyDictionary<string, string> ClientMetadata { get; set; }
    = new Dictionary<string, string>();

  public string TraceId { get; set; } = string.Empty;
}

public class ArchiveSheetIngestJob
{
  public Guid ArchiveId { get; set; }

  public Guid ArchiveSheetId { get; set; }

  public Guid BundleId { get; set; }

  public int SequenceInArchive { get; set; }

  public string TraceId { get; set; } = string.Empty;
}

public class FinalizeBundleJob
{
  public Guid BundleId { get; set; }

  public Guid ArchiveId { get; set; }

  public string TraceId { get; set; } = string.Empty;
}

public class QueueMessage<T>
{
  public string MessageId { get; set; } = string.Empty;

  public T Payload { get; set; } = default!;

  public int DeliveryCount { get; set; } = 1;

  public bool WasReclaimed { get; set; }
}
