using System.Collections.Generic;

namespace Scrinium.Core.Extraction;

public sealed class ExtractionResult
{
  public string Text { get; set; } = string.Empty;

  public Dictionary<string, string> Metadata { get; set; } = new();

  public int PageCount { get; set; } = 1;

  public IReadOnlyList<string> Barcodes { get; set; } = [];

  public Dictionary<string, object?> Warnings { get; set; } = new();
}

public sealed class NormalizeResult
{
  public byte[] PdfBytes { get; set; } = [];

  public bool WasConverted { get; set; }

  public string ContentType { get; set; } = "application/pdf";
}

public sealed class PageRenderResult
{
  public Dictionary<Rendering.RenderTier, string> ObjectKeys { get; set; } = new();

  public string? PlainText { get; set; }

  public bool HasTextLayer { get; set; }
}

public enum DocumentFormatKind
{
  Pdf = 0,
  Image = 1,
  Office = 2,
  Web = 3,
}
