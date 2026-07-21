using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEditionsVolumeCoverAndSeriesNameLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_NormalizedName",
                table: "Series");

            migrationBuilder.AddColumn<string>(
                name: "KPlusOverrides",
                table: "Volume",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MangaBakaEditionId",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NameLocked",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedOriginalName",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableName",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableVolumeCoverImage",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Series_LibraryId_Format_NormalizedLocalizedName",
                table: "Series",
                columns: new[] { "LibraryId", "Format", "NormalizedLocalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_LibraryId_Format_NormalizedName",
                table: "Series",
                columns: new[] { "LibraryId", "Format", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_LibraryId_Format_NormalizedOriginalName",
                table: "Series",
                columns: new[] { "LibraryId", "Format", "NormalizedOriginalName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_LibraryId_Format_NormalizedLocalizedName",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_LibraryId_Format_NormalizedName",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_LibraryId_Format_NormalizedOriginalName",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "KPlusOverrides",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MangaBakaEditionId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "NameLocked",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "NormalizedOriginalName",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "EnableName",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "EnableVolumeCoverImage",
                table: "MetadataSettings");

            migrationBuilder.CreateIndex(
                name: "IX_Series_NormalizedName",
                table: "Series",
                column: "NormalizedName");
        }
    }
}
