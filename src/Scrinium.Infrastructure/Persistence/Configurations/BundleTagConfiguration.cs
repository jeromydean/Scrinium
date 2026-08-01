using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class BundleTagConfiguration : IEntityTypeConfiguration<BundleTag>
{
  public void Configure(EntityTypeBuilder<BundleTag> builder)
  {
    builder.ToTable("bundle_tags");
    builder.HasKey(x => new { x.BundleId, x.TagId });
    builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);

    builder.HasOne(x => x.Bundle)
      .WithMany(x => x.BundleTags)
      .HasForeignKey(x => x.BundleId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.Tag)
      .WithMany(x => x.BundleTags)
      .HasForeignKey(x => x.TagId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
