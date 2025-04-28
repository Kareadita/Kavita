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

            migrationBuilder.CreateTable(
                name: "ExternalChapterMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalChapterMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalChapterMetadata_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalChapterReview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    BodyJustText = table.Column<string>(type: "TEXT", nullable: true),
                    RawBody = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Authority = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalChapterReview", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalChapterMetadataExternalChapterReview",
                columns: table => new
                {
                    ExternalChapterMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalReviewsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalChapterMetadataExternalChapterReview", x => new { x.ExternalChapterMetadatasId, x.ExternalReviewsId });
                    table.ForeignKey(
                        name: "FK_ExternalChapterMetadataExternalChapterReview_ExternalChapterMetadata_ExternalChapterMetadatasId",
                        column: x => x.ExternalChapterMetadatasId,
                        principalTable: "ExternalChapterMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalChapterMetadataExternalChapterReview_ExternalChapterReview_ExternalReviewsId",
                        column: x => x.ExternalReviewsId,
                        principalTable: "ExternalChapterReview",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChapterMetadata_ChapterId",
                table: "ExternalChapterMetadata",
                column: "ChapterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChapterMetadataExternalChapterReview_ExternalReviewsId",
                table: "ExternalChapterMetadataExternalChapterReview",
                column: "ExternalReviewsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserChapterRating");

            migrationBuilder.DropTable(
                name: "ExternalChapterMetadataExternalChapterReview");

            migrationBuilder.DropTable(
                name: "ExternalChapterMetadata");

            migrationBuilder.DropTable(
                name: "ExternalChapterReview");
        }
    }
}
