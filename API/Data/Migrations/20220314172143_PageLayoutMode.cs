using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class PageLayoutMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BookReaderDarkMode",
                table: "AppUserPreferences",
                newName: "PageLayoutMode");

            migrationBuilder.AlterColumn<string>(
                name: "BookThemeName",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "Dark",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PageLayoutMode",
                table: "AppUserPreferences",
                newName: "BookReaderDarkMode");

            migrationBuilder.AlterColumn<string>(
                name: "BookThemeName",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "Dark");
        }
    }
}
