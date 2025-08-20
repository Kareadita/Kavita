using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class BookAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChapterTitle",
                table: "AppUserTableOfContent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedText",
                table: "AppUserTableOfContent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookReaderHighlightSlots",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChapterTitle",
                table: "AppUserBookmark",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageOffset",
                table: "AppUserBookmark",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XPath",
                table: "AppUserBookmark",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppUserAnnotation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    XPath = table.Column<string>(type: "TEXT", nullable: true),
                    EndingXPath = table.Column<string>(type: "TEXT", nullable: true),
                    SelectedText = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    HighlightCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedSlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Context = table.Column<string>(type: "TEXT", nullable: true),
                    ContainsSpoiler = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChapterTitle = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserAnnotation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserAnnotation_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserAnnotation_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAnnotation_AppUserId",
                table: "AppUserAnnotation",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAnnotation_ChapterId",
                table: "AppUserAnnotation",
                column: "ChapterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserAnnotation");

            migrationBuilder.DropColumn(
                name: "ChapterTitle",
                table: "AppUserTableOfContent");

            migrationBuilder.DropColumn(
                name: "SelectedText",
                table: "AppUserTableOfContent");

            migrationBuilder.DropColumn(
                name: "BookReaderHighlightSlots",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "ChapterTitle",
                table: "AppUserBookmark");

            migrationBuilder.DropColumn(
                name: "ImageOffset",
                table: "AppUserBookmark");

            migrationBuilder.DropColumn(
                name: "XPath",
                table: "AppUserBookmark");
        }
    }
}
