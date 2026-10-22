using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class LibraryFolderWatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastScanned",
                table: "FolderPath");

            // The bool is stored as 0 or 1, which are FolderWatchMode.Off and FolderWatchMode.Automatic
            migrationBuilder.RenameColumn(
                name: "FolderWatching",
                table: "Library",
                newName: "FolderWatchMode");

            migrationBuilder.AddColumn<string>(
                name: "FolderWatchState",
                table: "Library",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"Mounts\":[],\"Downgrade\":null}");

            migrationBuilder.AddColumn<string>(
                name: "ScanInfo",
                table: "Library",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"Normal\":null,\"Forced\":null}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderWatchState",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "ScanInfo",
                table: "Library");

            migrationBuilder.RenameColumn(
                name: "FolderWatchMode",
                table: "Library",
                newName: "FolderWatching");

            migrationBuilder.Sql("UPDATE \"Library\" SET \"FolderWatching\" = 1 WHERE \"FolderWatching\" > 1;");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastScanned",
                table: "FolderPath",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
