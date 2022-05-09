using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class BookReaderUpdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BookReaderDarkMode",
                table: "AppUserPreferences",
                newName: "PageLayoutMode");

            migrationBuilder.AlterColumn<string>(
                name: "BackgroundColor",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "#000000",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookThemeName",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "Dark");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookThemeName",
                table: "AppUserPreferences");

            migrationBuilder.RenameColumn(
                name: "PageLayoutMode",
                table: "AppUserPreferences",
                newName: "BookReaderDarkMode");

            migrationBuilder.AlterColumn<string>(
                name: "BackgroundColor",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "#000000");
        }
    }
}
