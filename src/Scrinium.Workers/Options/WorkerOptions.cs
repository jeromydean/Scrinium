using System;

namespace Scrinium.Workers.Options;

public sealed class WorkerOptions
{
  public const string SectionName = "Workers";

  public string ConsumerName { get; set; } = Environment.MachineName;

  public int PollIntervalMs { get; set; } = 1000;

  public int MaxPageRetries { get; set; } = 3;

  /// <summary>
  /// Maximum delivery attempts before a job is marked failed and acknowledged.
  /// </summary>
  public int MaxJobAttempts { get; set; } = 5;
}
