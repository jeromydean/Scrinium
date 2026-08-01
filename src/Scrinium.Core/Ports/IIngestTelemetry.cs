using System;
using Scrinium.Core.Telemetry;

namespace Scrinium.Core.Ports;

public interface IIngestTelemetry
{
  IIngestStep BeginStep(IngestStepContext context);
}

public interface IIngestStep : IDisposable
{
  void CompleteSuccess();

  void CompleteError(string message);

  void RecordRetry(int attempt);
}
