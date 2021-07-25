using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class AddedSeriesFormat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId",
                table: "Series");

            migrationBuilder.AddColumn<int>(
                name: "Format",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId_Format",
                table: "Series",
                columns: new[] { "Name", "NormalizedName", "LocalizedName", "LibraryId", "Format" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId_Format",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Series");

            migrationBuilder.CreateIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId",
                table: "Series",
                columns: new[] { "Name", "NormalizedName", "LocalizedName", "LibraryId" },
                unique: true);
        }
    }
}
