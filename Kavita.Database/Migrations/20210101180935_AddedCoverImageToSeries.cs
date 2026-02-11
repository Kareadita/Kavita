using Microsoft.EntityFrameworkCore.Migrations;

namespace Kavita.Database.Migrations
{
    public partial class AddedCoverImageToSeries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
                table: "Series",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "Series");
        }
    }
}
