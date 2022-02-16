using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SiteTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SiteDarkMode",
                table: "AppUserPreferences");

            migrationBuilder.AddColumn<int>(
                name: "ThemeId",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SiteTheme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteTheme", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserPreferences_ThemeId",
                table: "AppUserPreferences",
                column: "ThemeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserPreferences_SiteTheme_ThemeId",
                table: "AppUserPreferences",
                column: "ThemeId",
                principalTable: "SiteTheme",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserPreferences_SiteTheme_ThemeId",
                table: "AppUserPreferences");

            migrationBuilder.DropTable(
                name: "SiteTheme");

            migrationBuilder.DropIndex(
                name: "IX_AppUserPreferences_ThemeId",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "ThemeId",
                table: "AppUserPreferences");

            migrationBuilder.AddColumn<bool>(
                name: "SiteDarkMode",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
