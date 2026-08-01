using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

internal static class IngestionJobRunner
{
  public static async Task RunAsync<T>(
    QueueMessage<T> message,
    string streamName,
    string consumerGroup,
    IJobQueue jobQueue,
    WorkerOptions options,
    ILogger logger,
    Func<T, CancellationToken, Task> processAsync,
    Func<T, string, CancellationToken, Task>? giveUpAsync,
    CancellationToken cancellationToken)
  {
    if (message.WasReclaimed)
    {
      logger.LogInformation(
        "Resuming reclaimed job {MessageId} on {StreamName} (delivery {DeliveryCount}).",
        message.MessageId,
        streamName,
        message.DeliveryCount);
    }

    try
    {
      await processAsync(message.Payload, cancellationToken);
      await jobQueue.AcknowledgeAsync(streamName, consumerGroup, message.MessageId, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      logger.LogInformation(
        "Job {MessageId} on {StreamName} interrupted by shutdown; will resume on next run.",
        message.MessageId,
        streamName);
      throw;
    }
    catch (Exception ex)
    {
      if (message.DeliveryCount >= options.MaxJobAttempts)
      {
        logger.LogError(
          ex,
          "Job {MessageId} on {StreamName} exceeded {MaxAttempts} attempts; giving up.",
          message.MessageId,
          streamName,
          options.MaxJobAttempts);

        if (giveUpAsync is not null)
        {
          await giveUpAsync(message.Payload, ex.Message, cancellationToken);
        }

        await jobQueue.AcknowledgeAsync(streamName, consumerGroup, message.MessageId, cancellationToken);
        return;
      }

      logger.LogWarning(
        ex,
        "Job {MessageId} on {StreamName} failed (delivery {DeliveryCount}/{MaxAttempts}); will retry.",
        message.MessageId,
        streamName,
        message.DeliveryCount,
        options.MaxJobAttempts);
    }
  }
}
