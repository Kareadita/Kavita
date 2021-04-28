using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class CustomChapterTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSpecial",
                table: "Volume");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Chapter",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Chapter");

            migrationBuilder.AddColumn<bool>(
                name: "IsSpecial",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
