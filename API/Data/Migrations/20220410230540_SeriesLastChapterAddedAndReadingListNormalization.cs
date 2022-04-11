using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SeriesLastChapterAddedAndReadingListNormalization : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastChapterAdded",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CoverImageLocked",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChapterAdded",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "CoverImageLocked",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                table: "ReadingList");
        }
    }
}
