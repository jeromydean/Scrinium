using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class DocumentPageConfiguration : IEntityTypeConfiguration<DocumentPage>
{
  public void Configure(EntityTypeBuilder<DocumentPage> builder)
  {
    builder.ToTable("document_pages");
    builder.HasKey(x => new { x.DocumentId, x.PageNumber });

    builder.Property(x => x.SourceKind).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.RenderStatus).HasConversion<string>().HasMaxLength(16);
    builder.Property(x => x.HocrObjectKey).HasMaxLength(1024);
    builder.Property(x => x.LastError).HasColumnType("text");

    builder.Property(x => x.HocrWords)
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<OcrWord>>(v, (JsonSerializerOptions?)null)
          ?? new List<OcrWord>());

    builder.HasOne(x => x.Document)
      .WithMany(x => x.Pages)
      .HasForeignKey(x => x.DocumentId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
