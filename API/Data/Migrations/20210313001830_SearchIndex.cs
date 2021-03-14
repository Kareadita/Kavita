using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class SearchIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName",
                table: "Series",
                columns: new[] { "Name", "NormalizedName", "LocalizedName" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName",
                table: "Series");
        }
    }
}
