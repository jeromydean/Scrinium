using System;
using System.Threading.Tasks;
using Scrinium.Core.Ports;
using Scrinium.Core.Telemetry;

namespace Scrinium.Workers.Ingestion;

internal static class IngestStepRunner
{
  public static async Task RunAsync(
    IIngestTelemetry telemetry,
    IngestStepContext context,
    Func<Task> action)
  {
    using IIngestStep step = telemetry.BeginStep(context);
    try
    {
      await action();
      step.CompleteSuccess();
    }
    catch (Exception ex)
    {
      step.CompleteError(ex.Message);
      throw;
    }
  }

  public static async Task<T> RunAsync<T>(
    IIngestTelemetry telemetry,
    IngestStepContext context,
    Func<Task<T>> action)
  {
    using IIngestStep step = telemetry.BeginStep(context);
    try
    {
      T result = await action();
      step.CompleteSuccess();
      return result;
    }
    catch (Exception ex)
    {
      step.CompleteError(ex.Message);
      throw;
    }
  }
}
