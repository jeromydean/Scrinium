using System;

namespace Scrinium.Core.Telemetry;

public sealed record IngestStepContext(
  Guid DocumentId,
  int? PageNumber,
  string StepName,
  string WorkerType,
  string WorkerId,
  string? TraceId);
