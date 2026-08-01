using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class ArchiveConfiguration : IEntityTypeConfiguration<Archive>
{
  public void Configure(EntityTypeBuilder<Archive> builder)
  {
    builder.ToTable("archives");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    builder.Property(x => x.ProcessingStep).HasMaxLength(64);
    builder.Property(x => x.OriginalFileName).HasMaxLength(512).IsRequired();
    builder.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
    builder.Property(x => x.IngestQuality).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.LastError).HasColumnType("text");
    builder.Property(x => x.IdempotencyKey).HasMaxLength(256);
    builder.Property(x => x.TraceId).HasMaxLength(128);
    builder.Property(x => x.StagingPath).HasMaxLength(1024);

    builder.Property(x => x.ExtractedMetadata)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
          ?? new Dictionary<string, string>());

    builder.Property(x => x.ClientMetadata)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
          ?? new Dictionary<string, string>());

    builder.Property(x => x.ExtractionWarnings)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null)
          ?? new Dictionary<string, object?>());

    builder.HasIndex(x => x.IdempotencyKey).IsUnique();
    builder.HasIndex(x => x.Status);
    builder.HasIndex(x => x.UploadedAt);
  }
}
