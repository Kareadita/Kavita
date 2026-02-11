using Microsoft.EntityFrameworkCore.Migrations;

namespace Kavita.Database.Migrations
{
    public partial class AddLocalizedName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalizedName",
                table: "Series",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalizedName",
                table: "Series");
        }
    }
}
