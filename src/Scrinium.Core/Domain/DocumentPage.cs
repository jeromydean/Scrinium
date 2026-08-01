using System;
using System.Collections.Generic;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class DocumentPage
{
  public Guid DocumentId { get; set; }

  public int PageNumber { get; set; }

  public int? FrameIndex { get; set; }

  public PageSourceKind SourceKind { get; set; }

  public string? PlainText { get; set; }

  public string? HocrObjectKey { get; set; }

  public List<OcrWord> HocrWords { get; set; } = new();

  public bool HasTextLayer { get; set; }

  public float? OcrConfidence { get; set; }

  public PageProcessingStatus ProcessingStatus { get; set; } = PageProcessingStatus.Pending;

  public PageProcessingStatus RenderStatus { get; set; } = PageProcessingStatus.Pending;

  public string? LastError { get; set; }

  public Document Document { get; set; } = null!;
}

public class OcrWord
{
  public string Text { get; set; } = string.Empty;

  public BoundingBox Bbox { get; set; } = new();

  public float Confidence { get; set; }
}

public class BoundingBox
{
  public int X { get; set; }

  public int Y { get; set; }

  public int W { get; set; }

  public int H { get; set; }
}
