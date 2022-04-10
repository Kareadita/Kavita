using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SeriesLastChapterAddedAndReadingListCoverImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastChapterAdded",
                table: "Series",
                type: "TEXT",
                nullable: false,
                defaultValue: DateTime.Now.Subtract(TimeSpan.FromDays(10)));

            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
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
        }
    }
}
