using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capstone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptionAndSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioTranscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullText = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioTranscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioTranscriptions_AudioFiles_AudioFileId",
                        column: x => x.AudioFileId,
                        principalTable: "AudioFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TranscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SummaryText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioSummaries_AudioFiles_AudioFileId",
                        column: x => x.AudioFileId,
                        principalTable: "AudioFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioSummaries_AudioTranscriptions_TranscriptionId",
                        column: x => x.TranscriptionId,
                        principalTable: "AudioTranscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioSummaries_AudioFileId",
                table: "AudioSummaries",
                column: "AudioFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSummaries_TranscriptionId",
                table: "AudioSummaries",
                column: "TranscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioTranscriptions_AudioFileId",
                table: "AudioTranscriptions",
                column: "AudioFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioSummaries");

            migrationBuilder.DropTable(
                name: "AudioTranscriptions");
        }
    }
}
