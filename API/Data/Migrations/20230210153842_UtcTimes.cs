using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class UtcTimes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Volume",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "Volume",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "SiteTheme",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "SiteTheme",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChapterAddedUtc",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFolderScannedUtc",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "MangaFile",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFileAnalysisUtc",
                table: "MangaFile",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "MangaFile",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Library",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "Library",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Device",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "Device",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedUtc",
                table: "Device",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Chapter",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "Chapter",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveUtc",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AppUserProgresses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "AppUserProgresses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AppUserBookmark",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "AppUserBookmark",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgresses_ChapterId",
                table: "AppUserProgresses",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserProgresses_Chapter_ChapterId",
                table: "AppUserProgresses",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserProgresses_Chapter_ChapterId",
                table: "AppUserProgresses");

            migrationBuilder.DropIndex(
                name: "IX_AppUserProgresses_ChapterId",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "LastChapterAddedUtc",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "LastFolderScannedUtc",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "LastFileAnalysisUtc",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "LastUsedUtc",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastActiveUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AppUserBookmark");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "AppUserBookmark");
        }
    }
}
