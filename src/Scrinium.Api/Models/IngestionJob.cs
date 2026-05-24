namespace Scrinium.Api.Models
{
  public sealed class IngestionJob
  {
    public required Guid DocumentId { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string StagingPath { get; init; }

    public required long FileSizeBytes { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
  }
}
