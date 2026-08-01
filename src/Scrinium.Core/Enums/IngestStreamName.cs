namespace Scrinium.Core.Enums;

public static class IngestStreamName
{
  public const string Archives = "ingest:archives";

  public const string Sheets = "ingest:sheets";

  public const string Finalize = "ingest:finalize";

  public const string DeadLetter = "ingest:dlq";
}
