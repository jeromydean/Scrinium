using System;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class DocumentTag
{
  public Guid DocumentId { get; set; }

  public Guid TagId { get; set; }

  public TagSource Source { get; set; }

  public DateTimeOffset AppliedAt { get; set; }

  public Document Document { get; set; } = null!;

  public Tag Tag { get; set; } = null!;
}
