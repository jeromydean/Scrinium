using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using StackExchange.Redis;

namespace Scrinium.Infrastructure.Queue;

public sealed class RedisOptions
{
  public const string SectionName = "Redis";

  public string ConnectionString { get; set; } = "localhost:6379";

  public string ConsumerGroupPrefix { get; set; } = "scrinium";

  /// <summary>
  /// Minimum idle time before a pending message is auto-claimed from another consumer.
  /// </summary>
  public int PendingMinIdleMs { get; set; } = 5000;
}

public sealed class RedisStreamJobQueue : IJobQueue
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  private readonly IConnectionMultiplexer _redis;
  private readonly RedisOptions _options;
  private readonly ILogger<RedisStreamJobQueue> _logger;

  public RedisStreamJobQueue(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisStreamJobQueue> logger)
  {
    _redis = redis;
    _options = options.Value;
    _logger = logger;
  }

  public async Task EnqueueAsync<T>(
    string streamName,
    T payload,
    CancellationToken cancellationToken)
  {
    IDatabase db = _redis.GetDatabase();
    string json = JsonSerializer.Serialize(payload, JsonOptions);

    RedisValue messageId = await db.StreamAddAsync(
      streamName,
      new NameValueEntry[]
      {
        new("payload", json),
      });

    _logger.LogDebug("Enqueued message {MessageId} on stream {StreamName}.", messageId, streamName);
  }

  public async Task<QueueMessage<T>?> DequeueAsync<T>(
    string streamName,
    string consumerGroup,
    string consumerName,
    CancellationToken cancellationToken)
  {
    IDatabase db = _redis.GetDatabase();
    await EnsureConsumerGroupAsync(streamName, consumerGroup, cancellationToken);

    (StreamEntry Entry, bool WasReclaimed)? pending = await TryReadPendingAsync(
      db,
      streamName,
      consumerGroup,
      consumerName);

    if (pending is null)
    {
      pending = await TryAutoClaimAsync(db, streamName, consumerGroup, consumerName);
    }

    StreamEntry entry;
    bool wasReclaimed;
    if (pending is not null)
    {
      entry = pending.Value.Entry;
      wasReclaimed = pending.Value.WasReclaimed;
    }
    else
    {
      StreamEntry[] entries = await db.StreamReadGroupAsync(
        streamName,
        consumerGroup,
        consumerName,
        position: ">",
        count: 1);

      if (entries.Length == 0)
      {
        return null;
      }

      entry = entries[0];
      wasReclaimed = false;
    }

    return await BuildMessageAsync<T>(db, streamName, consumerGroup, entry, wasReclaimed);
  }

  private async Task<(StreamEntry Entry, bool WasReclaimed)?> TryReadPendingAsync(
    IDatabase db,
    string streamName,
    string consumerGroup,
    string consumerName)
  {
    StreamEntry[] entries = await db.StreamReadGroupAsync(
      streamName,
      consumerGroup,
      consumerName,
      position: "0",
      count: 1);

    if (entries.Length == 0)
    {
      return null;
    }

    _logger.LogDebug(
      "Resuming pending message {MessageId} for consumer {ConsumerName} on {StreamName}.",
      entries[0].Id,
      consumerName,
      streamName);

    return (entries[0], WasReclaimed: true);
  }

  private async Task<(StreamEntry Entry, bool WasReclaimed)?> TryAutoClaimAsync(
    IDatabase db,
    string streamName,
    string consumerGroup,
    string consumerName)
  {
    StreamAutoClaimResult claimResult = await db.StreamAutoClaimAsync(
      streamName,
      consumerGroup,
      consumerName,
      _options.PendingMinIdleMs,
      "0-0",
      count: 1);

    if (claimResult.ClaimedEntries.Length == 0)
    {
      return null;
    }

    _logger.LogInformation(
      "Auto-claimed stale message {MessageId} on {StreamName} for consumer {ConsumerName}.",
      claimResult.ClaimedEntries[0].Id,
      streamName,
      consumerName);

    return (claimResult.ClaimedEntries[0], WasReclaimed: true);
  }

  private async Task<QueueMessage<T>> BuildMessageAsync<T>(
    IDatabase db,
    string streamName,
    string consumerGroup,
    StreamEntry entry,
    bool wasReclaimed)
  {
    NameValueEntry payloadEntry = entry.Values.FirstOrDefault(x => x.Name == "payload");
    string payloadJson = payloadEntry.Value.ToString();
    T? payload = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);

    if (payload is null)
    {
      throw new InvalidOperationException(
        $"Stream message {entry.Id} on {streamName} did not contain a valid payload.");
    }

    int deliveryCount = await GetDeliveryCountAsync(db, streamName, consumerGroup, entry.Id!);

    return new QueueMessage<T>
    {
      MessageId = entry.Id!,
      Payload = payload,
      DeliveryCount = deliveryCount,
      WasReclaimed = wasReclaimed,
    };
  }

  private static async Task<int> GetDeliveryCountAsync(
    IDatabase db,
    string streamName,
    string consumerGroup,
    string messageId)
  {
    StreamPendingMessageInfo[] pending = await db.StreamPendingMessagesAsync(
      streamName,
      consumerGroup,
      1,
      messageId,
      messageId);

    if (pending.Length == 0)
    {
      return 1;
    }

    return Math.Max(1, (int)pending[0].DeliveryCount);
  }

  public Task AcknowledgeAsync(
    string streamName,
    string consumerGroup,
    string messageId,
    CancellationToken cancellationToken)
  {
    IDatabase db = _redis.GetDatabase();
    return db.StreamAcknowledgeAsync(streamName, consumerGroup, messageId);
  }

  public async Task EnsureConsumerGroupAsync(
    string streamName,
    string consumerGroup,
    CancellationToken cancellationToken)
  {
    IDatabase db = _redis.GetDatabase();

    try
    {
      await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "$", createStream: true);
    }
    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
    {
      // Group already exists.
    }
  }
}
