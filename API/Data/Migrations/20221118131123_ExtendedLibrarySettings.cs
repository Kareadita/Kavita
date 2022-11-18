using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class ExtendedLibrarySettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FolderWatching",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInDashboard",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInRecommended",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInSearch",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderWatching",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "IncludeInDashboard",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "IncludeInRecommended",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "IncludeInSearch",
                table: "Library");
        }
    }
}
