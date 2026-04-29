using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AppUserReadingHistoryIndexChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingHistory_AppUserId",
                table: "AppUserReadingHistory");

            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingHistory_DateUtc",
                table: "AppUserReadingHistory");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingHistory_AppUserId_DateUtc",
                table: "AppUserReadingHistory",
                columns: new[] { "AppUserId", "DateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingHistory_AppUserId_DateUtc",
                table: "AppUserReadingHistory");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingHistory_AppUserId",
                table: "AppUserReadingHistory",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingHistory_DateUtc",
                table: "AppUserReadingHistory",
                column: "DateUtc",
                unique: true);
        }
    }
}
