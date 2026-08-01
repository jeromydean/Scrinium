namespace Scrinium.Core.Telemetry;

public static class ArchiveIngestSteps
{
  public const string StoreOriginal = "store_original";

  public const string Normalize = "normalize";

  public const string ExtractPdfMetadata = "extract_pdf_metadata";

  public const string ResolvePageCount = "resolve_page_count";

  public const string ExtractMetadata = "extract_metadata";

  public const string ExtractTikaText = "extract_tika_text";

  public const string ExtractionComplete = "extract_complete";

  public const string FanOutSheets = "fan_out_sheets";
}
