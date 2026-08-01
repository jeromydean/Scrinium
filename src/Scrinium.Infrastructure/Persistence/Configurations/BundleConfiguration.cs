using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class BundleConfiguration : IEntityTypeConfiguration<Bundle>
{
  public void Configure(EntityTypeBuilder<Bundle> builder)
  {
    builder.ToTable("bundles");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.IngestQuality).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.TraceId).HasMaxLength(128);

    builder.HasIndex(x => x.Status);
    builder.HasIndex(x => x.SourceArchiveId);

    builder.HasOne(x => x.SourceArchive)
      .WithMany(x => x.Bundles)
      .HasForeignKey(x => x.SourceArchiveId)
      .OnDelete(DeleteBehavior.SetNull);
  }
}
