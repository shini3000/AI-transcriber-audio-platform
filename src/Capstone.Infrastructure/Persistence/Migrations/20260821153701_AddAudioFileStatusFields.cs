using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capstone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFileStatusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AudioFiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "uploaded");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "AudioFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationErrorMessage",
                table: "AudioFiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "ValidationErrorMessage",
                table: "AudioFiles");
        }
    }
}
