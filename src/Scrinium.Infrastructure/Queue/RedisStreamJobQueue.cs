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

    StreamEntry entry = entries[0];
    NameValueEntry payloadEntry = entry.Values.FirstOrDefault(x => x.Name == "payload");
    string payloadJson = payloadEntry.Value.ToString();
    T? payload = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);

    if (payload is null)
    {
      throw new InvalidOperationException(
        $"Stream message {entry.Id} on {streamName} did not contain a valid payload.");
    }

    return new QueueMessage<T>
    {
      MessageId = entry.Id!,
      Payload = payload,
    };
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
