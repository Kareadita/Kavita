using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class PromptForRereadsAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                oldDefaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0}");

            migrationBuilder.AddColumn<int>(
                name: "PromptForRereadsAfter",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptForRereadsAfter",
                table: "AppUserPreferences");

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "AppUserReadingHistory",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"SeriesIds\":null,\"ChapterIds\":null}");
        }
    }
}
