using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddedMangaFileWriteTimesForChangedControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnalyzedFileWriteTimeUtc",
                table: "MangaFile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverFileWriteTimeUtc",
                table: "Chapter",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalyzedFileWriteTimeUtc",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "CoverFileWriteTimeUtc",
                table: "Chapter");
        }
    }
}
