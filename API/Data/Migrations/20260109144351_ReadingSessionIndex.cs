using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadingSessionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingSession_AppUserId",
                table: "AppUserReadingSession");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSession_AppUserId_IsActive",
                table: "AppUserReadingSession",
                columns: new[] { "AppUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSession_IsActive_LastModifiedUtc",
                table: "AppUserReadingSession",
                columns: new[] { "IsActive", "LastModifiedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingSession_AppUserId_IsActive",
                table: "AppUserReadingSession");

            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingSession_IsActive_LastModifiedUtc",
                table: "AppUserReadingSession");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSession_AppUserId",
                table: "AppUserReadingSession",
                column: "AppUserId");
        }
    }
}
