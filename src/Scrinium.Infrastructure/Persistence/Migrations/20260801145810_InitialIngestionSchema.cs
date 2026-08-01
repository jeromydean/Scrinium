using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrinium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIngestionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessingStep = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    PagesFailedCount = table.Column<int>(type: "integer", nullable: false),
                    IngestQuality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ExtractedMetadata = table.Column<string>(type: "jsonb", nullable: false),
                    ClientMetadata = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractionWarnings = table.Column<string>(type: "jsonb", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IngestStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IngestCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FinalizeEnqueued = table.Column<bool>(type: "boolean", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StagingPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingest_step_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    StepName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingest_step_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_pages",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    FrameIndex = table.Column<int>(type: "integer", nullable: true),
                    SourceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PlainText = table.Column<string>(type: "text", nullable: true),
                    HocrObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    HocrWords = table.Column<string>(type: "jsonb", nullable: false),
                    HasTextLayer = table.Column<bool>(type: "boolean", nullable: false),
                    OcrConfidence = table.Column<float>(type: "real", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RenderStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_pages", x => new { x.DocumentId, x.PageNumber });
                    table.ForeignKey(
                        name: "FK_document_pages_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_tags",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_tags", x => new { x.DocumentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_document_tags_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_tags_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_TagId",
                table: "document_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_IdempotencyKey",
                table: "documents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_Status",
                table: "documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_documents_UploadedAt",
                table: "documents",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ingest_step_log_DocumentId_StartedAt",
                table: "ingest_step_log",
                columns: new[] { "DocumentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ingest_step_log_TraceId",
                table: "ingest_step_log",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Name",
                table: "tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_pages");

            migrationBuilder.DropTable(
                name: "document_tags");

            migrationBuilder.DropTable(
                name: "ingest_step_log");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "tags");
        }
    }
}
