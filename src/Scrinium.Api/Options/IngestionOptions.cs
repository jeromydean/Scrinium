namespace Scrinium.Api.Options
{
  public sealed class IngestionOptions
  {
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Directory for uploaded files awaiting background processing.
    /// When empty, uses a subfolder under the system temp path.
    /// </summary>
    public string StagingPath { get; set; } = string.Empty;
  }
}
