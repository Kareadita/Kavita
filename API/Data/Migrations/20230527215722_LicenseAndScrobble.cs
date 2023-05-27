using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class LicenseAndScrobble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowScrobbling",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AniListAccessToken",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "License",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScrobbleEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScrobbleEventType = table.Column<int>(type: "INTEGER", nullable: false),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: true),
                    Rating = table.Column<float>(type: "REAL", nullable: true),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    VolumeNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleEvent_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrobbleEvent_Library_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Library",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrobbleEvent_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncHistory",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncHistory", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleEvent_AppUserId",
                table: "ScrobbleEvent",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleEvent_LibraryId",
                table: "ScrobbleEvent",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleEvent_SeriesId",
                table: "ScrobbleEvent",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrobbleEvent");

            migrationBuilder.DropTable(
                name: "SyncHistory");

            migrationBuilder.DropColumn(
                name: "AllowScrobbling",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "AniListAccessToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "License",
                table: "AspNetUsers");
        }
    }
}
