namespace Scrinium.Core.Enums;

public static class IngestStreamName
{
  public const string Documents = "ingest:documents";
  public const string Pages = "ingest:pages";
  public const string Finalize = "ingest:finalize";
  public const string DeadLetter = "ingest:dlq";
}
