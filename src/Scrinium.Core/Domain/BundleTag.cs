using System;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Domain;

public class BundleTag
{
  public Guid BundleId { get; set; }

  public Guid TagId { get; set; }

  public TagSource Source { get; set; }

  public DateTimeOffset AppliedAt { get; set; }

  public Bundle Bundle { get; set; } = null!;

  public Tag Tag { get; set; } = null!;
}
