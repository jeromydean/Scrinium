using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class ArchiveSheetConfiguration : IEntityTypeConfiguration<ArchiveSheet>
{
  public void Configure(EntityTypeBuilder<ArchiveSheet> builder)
  {
    builder.ToTable("archive_sheets");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.SourceKind).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.PlainText).HasColumnType("text");
    builder.Property(x => x.HocrObjectKey).HasMaxLength(1024);
    builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.RenderStatus).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.LastError).HasColumnType("text");

    builder.Property(x => x.HocrWords)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<OcrWord>>(v, (JsonSerializerOptions?)null)
          ?? new List<OcrWord>());

    builder.HasIndex(x => new { x.ArchiveId, x.SequenceInArchive }).IsUnique();

    builder.HasOne(x => x.Archive)
      .WithMany(x => x.ArchiveSheets)
      .HasForeignKey(x => x.ArchiveId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
