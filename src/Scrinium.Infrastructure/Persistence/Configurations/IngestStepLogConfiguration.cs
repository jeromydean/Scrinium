using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class IngestStepLogConfiguration : IEntityTypeConfiguration<IngestStepLog>
{
  public void Configure(EntityTypeBuilder<IngestStepLog> builder)
  {
    builder.ToTable("ingest_step_log");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.StepName).HasMaxLength(64).IsRequired();
    builder.Property(x => x.WorkerType).HasMaxLength(32).IsRequired();
    builder.Property(x => x.WorkerId).HasMaxLength(128).IsRequired();
    builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
    builder.Property(x => x.ErrorMessage).HasColumnType("text");
    builder.Property(x => x.TraceId).HasMaxLength(128);

    builder.Property(x => x.Metadata)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null)
          ?? new Dictionary<string, object?>());

    builder.HasIndex(x => new { x.ArchiveId, x.StartedAt });
    builder.HasIndex(x => new { x.ArchiveSheetId, x.StartedAt });
    builder.HasIndex(x => new { x.BundleId, x.StartedAt });
    builder.HasIndex(x => x.TraceId);
  }
}
