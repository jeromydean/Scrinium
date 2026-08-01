using System;

namespace Scrinium.Core.Domain;

public class SheetBarcode
{
  public Guid Id { get; set; }

  public Guid ArchiveSheetId { get; set; }

  public string Symbology { get; set; } = string.Empty;

  public string Value { get; set; } = string.Empty;

  public BoundingBox BoundingBox { get; set; } = new();

  public float Confidence { get; set; }

  public ArchiveSheet ArchiveSheet { get; set; } = null!;
}
