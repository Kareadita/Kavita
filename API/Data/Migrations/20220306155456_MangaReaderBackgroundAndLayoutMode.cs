using API.Entities.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class MangaReaderBackgroundAndLayoutMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "AppUserPreferences",
                type: "TEXT",
                defaultValue: "#000000",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "LayoutMode",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: LayoutMode.Single);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "LayoutMode",
                table: "AppUserPreferences");
        }
    }
}
