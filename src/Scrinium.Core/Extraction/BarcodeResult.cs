using Scrinium.Core.Domain;

namespace Scrinium.Core.Extraction;

public sealed class BarcodeResult
{
  public string Symbology { get; set; } = string.Empty;

  public string Value { get; set; } = string.Empty;

  public BoundingBox Bbox { get; set; } = new();

  public float Confidence { get; set; }
}
