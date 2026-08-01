using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
  public void Configure(EntityTypeBuilder<Tag> builder)
  {
    builder.ToTable("tags");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
    builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
    builder.Property(x => x.Color).HasMaxLength(32);
    builder.HasIndex(x => x.Name).IsUnique();
  }
}

internal sealed class DocumentTagConfiguration : IEntityTypeConfiguration<DocumentTag>
{
  public void Configure(EntityTypeBuilder<DocumentTag> builder)
  {
    builder.ToTable("document_tags");
    builder.HasKey(x => new { x.DocumentId, x.TagId });
    builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);

    builder.HasOne(x => x.Document)
      .WithMany(x => x.DocumentTags)
      .HasForeignKey(x => x.DocumentId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.Tag)
      .WithMany(x => x.DocumentTags)
      .HasForeignKey(x => x.TagId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
