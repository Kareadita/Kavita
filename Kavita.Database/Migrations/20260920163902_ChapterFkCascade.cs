using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChapterFkCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRating_Chapter_ChapterId",
                table: "ExternalRating");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalReview_Chapter_ChapterId",
                table: "ExternalReview");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRating_Chapter_ChapterId",
                table: "ExternalRating",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalReview_Chapter_ChapterId",
                table: "ExternalReview",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRating_Chapter_ChapterId",
                table: "ExternalRating");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalReview_Chapter_ChapterId",
                table: "ExternalReview");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRating_Chapter_ChapterId",
                table: "ExternalRating",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalReview_Chapter_ChapterId",
                table: "ExternalReview",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");
        }
    }
}
