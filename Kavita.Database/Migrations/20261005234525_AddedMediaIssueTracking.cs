using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddedMediaIssueTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old scanner rows hold a file name only, with no library or stamp to match against
            migrationBuilder.Sql("DELETE FROM \"MediaError\";");

            migrationBuilder.AddColumn<long>(
                name: "Bytes",
                table: "MediaError",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FileLastWriteTimeUtc",
                table: "MediaError",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDismissed",
                table: "MediaError",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenUtc",
                table: "MediaError",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "LibraryId",
                table: "MediaError",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Producer",
                table: "MediaError",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "MediaError",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "MediaError",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaError_LibraryId",
                table: "MediaError",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaError_SeriesId",
                table: "MediaError",
                column: "SeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaError_Library_LibraryId",
                table: "MediaError",
                column: "LibraryId",
                principalTable: "Library",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaError_Series_SeriesId",
                table: "MediaError",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaError_Library_LibraryId",
                table: "MediaError");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaError_Series_SeriesId",
                table: "MediaError");

            migrationBuilder.DropIndex(
                name: "IX_MediaError_LibraryId",
                table: "MediaError");

            migrationBuilder.DropIndex(
                name: "IX_MediaError_SeriesId",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "FileLastWriteTimeUtc",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "IsDismissed",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "LastSeenUtc",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "Producer",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "MediaError");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "MediaError");
        }
    }
}
