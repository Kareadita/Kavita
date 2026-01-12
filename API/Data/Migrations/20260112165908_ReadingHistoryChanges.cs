using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadingHistoryChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AppUserReadingHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "AppUserReadingHistory",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"Activities\":[],\"SeriesIds\":null,\"ChapterIds\":null}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"SeriesIds\":null,\"ChapterIds\":null}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "AppUserReadingHistory",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"SeriesIds\":null,\"ChapterIds\":null}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"Activities\":[],\"SeriesIds\":null,\"ChapterIds\":null}");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AppUserReadingHistory",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
