using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capstone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompressedAt",
                table: "AudioFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CompressionDurationMs",
                table: "AudioFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalContentType",
                table: "AudioFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "AudioFiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "OriginalSize",
                table: "AudioFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompressedAt",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "CompressionDurationMs",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "OriginalContentType",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "OriginalSize",
                table: "AudioFiles");
        }
    }
}
