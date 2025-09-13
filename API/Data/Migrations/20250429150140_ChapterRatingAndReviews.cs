using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChapterRatingAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Authority",
                table: "ExternalReview",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "ExternalReview",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                table: "ExternalRating",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "ExternalRating",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "AverageExternalRating",
                table: "Chapter",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateTable(
                name: "AppUserChapterRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Rating = table.Column<float>(type: "REAL", nullable: false),
                    HasBeenRated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Review = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserChapterRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserChapterRating_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserChapterRating_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserChapterRating_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReview_ChapterId",
                table: "ExternalReview",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRating_ChapterId",
                table: "ExternalRating",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserChapterRating_AppUserId",
                table: "AppUserChapterRating",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserChapterRating_ChapterId",
                table: "AppUserChapterRating",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserChapterRating_SeriesId",
                table: "AppUserChapterRating",
                column: "SeriesId");

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

            migrationBuilder.DropTable(
                name: "AppUserChapterRating");

            migrationBuilder.DropIndex(
                name: "IX_ExternalReview_ChapterId",
                table: "ExternalReview");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRating_ChapterId",
                table: "ExternalRating");

            migrationBuilder.DropColumn(
                name: "Authority",
                table: "ExternalReview");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "ExternalReview");

            migrationBuilder.DropColumn(
                name: "Authority",
                table: "ExternalRating");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "ExternalRating");

            migrationBuilder.DropColumn(
                name: "AverageExternalRating",
                table: "Chapter");
        }
    }
}
