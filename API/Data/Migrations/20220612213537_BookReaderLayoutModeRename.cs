using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class BookReaderLayoutModeRename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PageLayoutMode",
                table: "AppUserPreferences",
                newName: "GlobalPageLayoutMode");

            migrationBuilder.AddColumn<int>(
                name: "BookReaderLayoutMode",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookReaderLayoutMode",
                table: "AppUserPreferences");

            migrationBuilder.RenameColumn(
                name: "GlobalPageLayoutMode",
                table: "AppUserPreferences",
                newName: "PageLayoutMode");
        }
    }
}
