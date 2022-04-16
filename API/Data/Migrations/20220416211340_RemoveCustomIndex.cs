using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class RemoveCustomIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId_Format",
                table: "Series");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId_Format",
                table: "Series",
                columns: new[] { "Name", "NormalizedName", "LocalizedName", "LibraryId", "Format" },
                unique: true);
        }
    }
}
