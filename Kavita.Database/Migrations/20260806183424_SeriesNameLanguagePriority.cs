using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class SeriesNameLanguagePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GlobalLocalizedNameLanguages",
                table: "MetadataSettings",
                type: "TEXT",
                nullable: true,
                defaultValue: "ja-Latn");

            migrationBuilder.AddColumn<string>(
                name: "GlobalNameLanguages",
                table: "MetadataSettings",
                type: "TEXT",
                nullable: true,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "LibraryLanguageTitleOverrides",
                table: "MetadataSettings",
                type: "TEXT",
                nullable: true,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalLocalizedNameLanguages",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "GlobalNameLanguages",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "LibraryLanguageTitleOverrides",
                table: "MetadataSettings");
        }
    }
}
