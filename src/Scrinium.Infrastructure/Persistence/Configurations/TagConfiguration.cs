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
