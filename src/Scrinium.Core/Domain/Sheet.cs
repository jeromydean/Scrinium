using System;

namespace Scrinium.Core.Domain;

/// <summary>Membership of an <see cref="ArchiveSheet"/> in a <see cref="Bundle"/>.</summary>
public class Sheet
{
  public Guid BundleId { get; set; }

  public Guid ArchiveSheetId { get; set; }

  public int SortOrder { get; set; }

  public Bundle Bundle { get; set; } = null!;

  public ArchiveSheet ArchiveSheet { get; set; } = null!;
}
