namespace Scrinium.Core.Telemetry;

public static class ArchiveSheetIngestSteps
{
  public const string LoadSource = "load_sheet_source";

  public const string Rasterize = "rasterize_sheet";

  public const string DecodeImage = "decode_image";

  public const string UploadRenders = "upload_sheet_renders";

  public const string ExtractText = "extract_sheet_text";

  public const string Ocr = "ocr_sheet";

  public const string ScanBarcodes = "scan_sheet_barcodes";
}
