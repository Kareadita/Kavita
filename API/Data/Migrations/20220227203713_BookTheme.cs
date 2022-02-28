using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class BookTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookThemeId",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookTheme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: true),
                    ColorHash = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDarkTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookTheme", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserPreferences_BookThemeId",
                table: "AppUserPreferences",
                column: "BookThemeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserPreferences_BookTheme_BookThemeId",
                table: "AppUserPreferences",
                column: "BookThemeId",
                principalTable: "BookTheme",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserPreferences_BookTheme_BookThemeId",
                table: "AppUserPreferences");

            migrationBuilder.DropTable(
                name: "BookTheme");

            migrationBuilder.DropIndex(
                name: "IX_AppUserPreferences_BookThemeId",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "BookThemeId",
                table: "AppUserPreferences");
        }
    }
}
