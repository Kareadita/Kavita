using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class BookReaderPreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HideReadOnDetails",
                table: "AppUserPreferences",
                newName: "BookReaderMargin");

            migrationBuilder.AddColumn<bool>(
                name: "BookReaderDarkMode",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BookReaderFontFamily",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "default");

            migrationBuilder.AddColumn<int>(
                name: "BookReaderLineSpacing",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookReaderDarkMode",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "BookReaderFontFamily",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "BookReaderLineSpacing",
                table: "AppUserPreferences");

            migrationBuilder.RenameColumn(
                name: "BookReaderMargin",
                table: "AppUserPreferences",
                newName: "HideReadOnDetails");
        }
    }
}
