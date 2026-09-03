using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capstone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredAudioUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalStorageUrl",
                table: "AudioFiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilteredStorageUrl",
                table: "AudioFiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalStorageUrl",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "FilteredStorageUrl",
                table: "AudioFiles");
        }
    }
}
