using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class LibraryDefaultLanguageCustomKeyBinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                table: "Library",
                type: "TEXT",
                nullable: true,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomKeyBinds",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "CustomKeyBinds",
                table: "AppUserPreferences");
        }
    }
}
