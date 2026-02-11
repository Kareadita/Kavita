using Microsoft.EntityFrameworkCore.Migrations;

namespace Kavita.Database.Migrations
{
    public partial class CollectionSummary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "CollectionTag",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Summary",
                table: "CollectionTag");
        }
    }
}
