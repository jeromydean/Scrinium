using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class SheetConfiguration : IEntityTypeConfiguration<Sheet>
{
  public void Configure(EntityTypeBuilder<Sheet> builder)
  {
    builder.ToTable("sheets");
    builder.HasKey(x => new { x.BundleId, x.ArchiveSheetId });

    builder.HasIndex(x => new { x.BundleId, x.SortOrder }).IsUnique();

    builder.HasOne(x => x.Bundle)
      .WithMany(x => x.Sheets)
      .HasForeignKey(x => x.BundleId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.ArchiveSheet)
      .WithMany(x => x.BundleMemberships)
      .HasForeignKey(x => x.ArchiveSheetId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
