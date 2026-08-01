using System;
using System.Collections.Generic;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;

namespace Scrinium.Core.Ports;

public sealed class BundleListQuery
{
  public int Skip { get; set; }

  public int Take { get; set; } = 50;

  public BundleStatus? Status { get; set; }

  public string? Search { get; set; }
}

public sealed class BundleListResult
{
  public IReadOnlyList<Bundle> Items { get; set; } = Array.Empty<Bundle>();

  public int TotalCount { get; set; }
}
