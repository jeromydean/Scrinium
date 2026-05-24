namespace Scrinium.Api.Models
{
  public sealed class IngestionAcceptedResponse
  {
    public required Guid DocumentId { get; init; }

    public required string Status { get; init; }

    public required string FileName { get; init; }
  }
}
