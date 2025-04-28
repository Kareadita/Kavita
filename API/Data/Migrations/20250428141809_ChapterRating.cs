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
                name: "IX_AppUserRating_ChapterId",
                table: "AppUserRating",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChapterMetadata_ChapterId",
                table: "ExternalChapterMetadata",
                column: "ChapterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChapterMetadataExternalChapterReview_ExternalReviewsId",
                table: "ExternalChapterMetadataExternalChapterReview",
                column: "ExternalReviewsId");

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

            migrationBuilder.DropTable(
                name: "ExternalChapterMetadataExternalChapterReview");

            migrationBuilder.DropTable(
                name: "ExternalChapterMetadata");

            migrationBuilder.DropTable(
                name: "ExternalChapterReview");

            migrationBuilder.DropIndex(
                name: "IX_AppUserRating_ChapterId",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "AppUserRating");
        }
    }
}
