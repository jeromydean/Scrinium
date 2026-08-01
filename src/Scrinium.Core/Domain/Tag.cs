using System;
using System.Collections.Generic;

namespace Scrinium.Core.Domain;

public class Tag
{
  public Guid Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public string DisplayName { get; set; } = string.Empty;

  public string? Color { get; set; }

  public Guid CreatedBy { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public ICollection<BundleTag> BundleTags { get; set; } = new List<BundleTag>();
}
