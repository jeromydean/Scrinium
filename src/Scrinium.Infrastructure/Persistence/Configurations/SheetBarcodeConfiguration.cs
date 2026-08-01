using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence.Configurations;

internal sealed class SheetBarcodeConfiguration : IEntityTypeConfiguration<SheetBarcode>
{
  public void Configure(EntityTypeBuilder<SheetBarcode> builder)
  {
    builder.ToTable("sheet_barcodes");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.Symbology).HasMaxLength(64).IsRequired();
    builder.Property(x => x.Value).HasMaxLength(2048).IsRequired();
    builder.Property(x => x.BoundingBox)
      .HasColumnName("bounding_box")
      .HasColumnType("jsonb")
      .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<BoundingBox>(v, (JsonSerializerOptions?)null)
          ?? new BoundingBox());

    builder.HasIndex(x => x.ArchiveSheetId);
    builder.HasIndex(x => x.Value);

    builder.HasOne(x => x.ArchiveSheet)
      .WithMany(x => x.Barcodes)
      .HasForeignKey(x => x.ArchiveSheetId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
