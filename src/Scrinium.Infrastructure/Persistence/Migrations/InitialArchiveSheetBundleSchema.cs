using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrinium.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ScriniumDbContext))]
[Migration("20260801193709_InitialArchiveSheetBundleSchema")]
public partial class InitialArchiveSheetBundleSchema : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
      name: "archives",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
        ProcessingStep = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
        OriginalFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
        ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
        ByteSize = table.Column<long>(type: "bigint", nullable: false),
        SheetCount = table.Column<int>(type: "integer", nullable: false),
        SheetsFailedCount = table.Column<int>(type: "integer", nullable: false),
        IngestQuality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
        ExtractedMetadata = table.Column<string>(type: "jsonb", nullable: false),
        ClientMetadata = table.Column<string>(type: "jsonb", nullable: false),
        ExtractionWarnings = table.Column<string>(type: "jsonb", nullable: false),
        UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
        UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
        IngestStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        IngestCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        LastError = table.Column<string>(type: "text", nullable: true),
        IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
        TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
        StagingPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
      },
      constraints: table => table.PrimaryKey("PK_archives", x => x.Id));

    migrationBuilder.CreateTable(
      name: "ingest_step_log",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        ArchiveId = table.Column<Guid>(type: "uuid", nullable: true),
        ArchiveSheetId = table.Column<Guid>(type: "uuid", nullable: true),
        BundleId = table.Column<Guid>(type: "uuid", nullable: true),
        StepName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
        WorkerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
        WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
        StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
        CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        DurationMs = table.Column<long>(type: "bigint", nullable: true),
        Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        ErrorMessage = table.Column<string>(type: "text", nullable: true),
        TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
        Metadata = table.Column<string>(type: "jsonb", nullable: false),
      },
      constraints: table => table.PrimaryKey("PK_ingest_step_log", x => x.Id));

    migrationBuilder.CreateTable(
      name: "tags",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
        DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
        Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
        CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
        CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
      },
      constraints: table => table.PrimaryKey("PK_tags", x => x.Id));

    migrationBuilder.CreateTable(
      name: "archive_sheets",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        ArchiveId = table.Column<Guid>(type: "uuid", nullable: false),
        SequenceInArchive = table.Column<int>(type: "integer", nullable: false),
        SourceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        PlainText = table.Column<string>(type: "text", nullable: true),
        HocrObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
        HocrWords = table.Column<string>(type: "jsonb", nullable: false),
        HasTextLayer = table.Column<bool>(type: "boolean", nullable: false),
        OcrConfidence = table.Column<float>(type: "real", nullable: true),
        ProcessingStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        RenderStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        LastError = table.Column<string>(type: "text", nullable: true),
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_archive_sheets", x => x.Id);
        table.ForeignKey(
          name: "FK_archive_sheets_archives_ArchiveId",
          column: x => x.ArchiveId,
          principalTable: "archives",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
      });

    migrationBuilder.CreateTable(
      name: "bundles",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
        Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        IsDefault = table.Column<bool>(type: "boolean", nullable: false),
        SourceArchiveId = table.Column<Guid>(type: "uuid", nullable: true),
        SheetsFailedCount = table.Column<int>(type: "integer", nullable: false),
        IngestQuality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
        CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
        CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
        ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        FinalizeEnqueued = table.Column<bool>(type: "boolean", nullable: false),
        TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_bundles", x => x.Id);
        table.ForeignKey(
          name: "FK_bundles_archives_SourceArchiveId",
          column: x => x.SourceArchiveId,
          principalTable: "archives",
          principalColumn: "Id",
          onDelete: ReferentialAction.SetNull);
      });

    migrationBuilder.CreateTable(
      name: "sheet_barcodes",
      columns: table => new
      {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        ArchiveSheetId = table.Column<Guid>(type: "uuid", nullable: false),
        Symbology = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
        Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
        bounding_box = table.Column<string>(type: "jsonb", nullable: false),
        Confidence = table.Column<float>(type: "real", nullable: false),
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_sheet_barcodes", x => x.Id);
        table.ForeignKey(
          name: "FK_sheet_barcodes_archive_sheets_ArchiveSheetId",
          column: x => x.ArchiveSheetId,
          principalTable: "archive_sheets",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
      });

    migrationBuilder.CreateTable(
      name: "sheets",
      columns: table => new
      {
        BundleId = table.Column<Guid>(type: "uuid", nullable: false),
        ArchiveSheetId = table.Column<Guid>(type: "uuid", nullable: false),
        SortOrder = table.Column<int>(type: "integer", nullable: false),
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_sheets", x => new { x.BundleId, x.ArchiveSheetId });
        table.ForeignKey(
          name: "FK_sheets_archive_sheets_ArchiveSheetId",
          column: x => x.ArchiveSheetId,
          principalTable: "archive_sheets",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
        table.ForeignKey(
          name: "FK_sheets_bundles_BundleId",
          column: x => x.BundleId,
          principalTable: "bundles",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
      });

    migrationBuilder.CreateTable(
      name: "bundle_tags",
      columns: table => new
      {
        BundleId = table.Column<Guid>(type: "uuid", nullable: false),
        TagId = table.Column<Guid>(type: "uuid", nullable: false),
        Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
        AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_bundle_tags", x => new { x.BundleId, x.TagId });
        table.ForeignKey(
          name: "FK_bundle_tags_bundles_BundleId",
          column: x => x.BundleId,
          principalTable: "bundles",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
        table.ForeignKey(
          name: "FK_bundle_tags_tags_TagId",
          column: x => x.TagId,
          principalTable: "tags",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
      });

    migrationBuilder.CreateIndex(name: "IX_archive_sheets_ArchiveId_SequenceInArchive", table: "archive_sheets", columns: new[] { "ArchiveId", "SequenceInArchive" }, unique: true);
    migrationBuilder.CreateIndex(name: "IX_archives_IdempotencyKey", table: "archives", column: "IdempotencyKey", unique: true);
    migrationBuilder.CreateIndex(name: "IX_archives_Status", table: "archives", column: "Status");
    migrationBuilder.CreateIndex(name: "IX_archives_UploadedAt", table: "archives", column: "UploadedAt");
    migrationBuilder.CreateIndex(name: "IX_bundle_tags_TagId", table: "bundle_tags", column: "TagId");
    migrationBuilder.CreateIndex(name: "IX_bundles_SourceArchiveId", table: "bundles", column: "SourceArchiveId");
    migrationBuilder.CreateIndex(name: "IX_bundles_Status", table: "bundles", column: "Status");
    migrationBuilder.CreateIndex(name: "IX_ingest_step_log_ArchiveId_StartedAt", table: "ingest_step_log", columns: new[] { "ArchiveId", "StartedAt" });
    migrationBuilder.CreateIndex(name: "IX_ingest_step_log_ArchiveSheetId_StartedAt", table: "ingest_step_log", columns: new[] { "ArchiveSheetId", "StartedAt" });
    migrationBuilder.CreateIndex(name: "IX_ingest_step_log_BundleId_StartedAt", table: "ingest_step_log", columns: new[] { "BundleId", "StartedAt" });
    migrationBuilder.CreateIndex(name: "IX_ingest_step_log_TraceId", table: "ingest_step_log", column: "TraceId");
    migrationBuilder.CreateIndex(name: "IX_sheet_barcodes_ArchiveSheetId", table: "sheet_barcodes", column: "ArchiveSheetId");
    migrationBuilder.CreateIndex(name: "IX_sheet_barcodes_Value", table: "sheet_barcodes", column: "Value");
    migrationBuilder.CreateIndex(name: "IX_sheets_ArchiveSheetId", table: "sheets", column: "ArchiveSheetId");
    migrationBuilder.CreateIndex(name: "IX_sheets_BundleId_SortOrder", table: "sheets", columns: new[] { "BundleId", "SortOrder" }, unique: true);
    migrationBuilder.CreateIndex(name: "IX_tags_Name", table: "tags", column: "Name", unique: true);
  }

  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(name: "bundle_tags");
    migrationBuilder.DropTable(name: "ingest_step_log");
    migrationBuilder.DropTable(name: "sheet_barcodes");
    migrationBuilder.DropTable(name: "sheets");
    migrationBuilder.DropTable(name: "tags");
    migrationBuilder.DropTable(name: "archive_sheets");
    migrationBuilder.DropTable(name: "bundles");
    migrationBuilder.DropTable(name: "archives");
  }
}
