using System;

namespace Scrinium.Core.Telemetry;

public sealed record IngestStepContext(
  Guid? ArchiveId,
  Guid? ArchiveSheetId,
  Guid? BundleId,
  string StepName,
  string WorkerType,
  string WorkerId,
  string? TraceId);
