using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChapterRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "AppUserRating",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRating_ChapterId",
                table: "AppUserRating",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserRating_Chapter_ChapterId",
                table: "AppUserRating",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserRating_Chapter_ChapterId",
                table: "AppUserRating");

            migrationBuilder.DropIndex(
                name: "IX_AppUserRating_ChapterId",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "AppUserRating");
        }
    }
}
