using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExternalSeriesMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AverageScore = table.Column<int>(type: "INTEGER", nullable: false),
                    FavoriteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRating", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRecommendation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: true),
                    MalId = table.Column<long>(type: "INTEGER", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRecommendation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRecommendation_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExternalReview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    BodyJustText = table.Column<string>(type: "TEXT", nullable: true),
                    RawBody = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReview", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalSeriesMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AverageExternalRating = table.Column<int>(type: "INTEGER", nullable: false),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: false),
                    MalId = table.Column<long>(type: "INTEGER", nullable: false),
                    GoogleBooksId = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSeriesMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalSeriesMetadata_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRatingExternalSeriesMetadata",
                columns: table => new
                {
                    ExternalRatingsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalSeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRatingExternalSeriesMetadata", x => new { x.ExternalRatingsId, x.ExternalSeriesMetadatasId });
                    table.ForeignKey(
                        name: "FK_ExternalRatingExternalSeriesMetadata_ExternalRating_ExternalRatingsId",
                        column: x => x.ExternalRatingsId,
                        principalTable: "ExternalRating",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalRatingExternalSeriesMetadata_ExternalSeriesMetadata_ExternalSeriesMetadatasId",
                        column: x => x.ExternalSeriesMetadatasId,
                        principalTable: "ExternalSeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRecommendationExternalSeriesMetadata",
                columns: table => new
                {
                    ExternalRecommendationsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalSeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRecommendationExternalSeriesMetadata", x => new { x.ExternalRecommendationsId, x.ExternalSeriesMetadatasId });
                    table.ForeignKey(
                        name: "FK_ExternalRecommendationExternalSeriesMetadata_ExternalRecommendation_ExternalRecommendationsId",
                        column: x => x.ExternalRecommendationsId,
                        principalTable: "ExternalRecommendation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalRecommendationExternalSeriesMetadata_ExternalSeriesMetadata_ExternalSeriesMetadatasId",
                        column: x => x.ExternalSeriesMetadatasId,
                        principalTable: "ExternalSeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalReviewExternalSeriesMetadata",
                columns: table => new
                {
                    ExternalReviewsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalSeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReviewExternalSeriesMetadata", x => new { x.ExternalReviewsId, x.ExternalSeriesMetadatasId });
                    table.ForeignKey(
                        name: "FK_ExternalReviewExternalSeriesMetadata_ExternalReview_ExternalReviewsId",
                        column: x => x.ExternalReviewsId,
                        principalTable: "ExternalReview",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalReviewExternalSeriesMetadata_ExternalSeriesMetadata_ExternalSeriesMetadatasId",
                        column: x => x.ExternalSeriesMetadatasId,
                        principalTable: "ExternalSeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRatingExternalSeriesMetadata_ExternalSeriesMetadatasId",
                table: "ExternalRatingExternalSeriesMetadata",
                column: "ExternalSeriesMetadatasId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRecommendation_SeriesId",
                table: "ExternalRecommendation",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRecommendationExternalSeriesMetadata_ExternalSeriesMetadatasId",
                table: "ExternalRecommendationExternalSeriesMetadata",
                column: "ExternalSeriesMetadatasId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReviewExternalSeriesMetadata_ExternalSeriesMetadatasId",
                table: "ExternalReviewExternalSeriesMetadata",
                column: "ExternalSeriesMetadatasId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSeriesMetadata_SeriesId",
                table: "ExternalSeriesMetadata",
                column: "SeriesId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalRatingExternalSeriesMetadata");

            migrationBuilder.DropTable(
                name: "ExternalRecommendationExternalSeriesMetadata");

            migrationBuilder.DropTable(
                name: "ExternalReviewExternalSeriesMetadata");

            migrationBuilder.DropTable(
                name: "ExternalRating");

            migrationBuilder.DropTable(
                name: "ExternalRecommendation");

            migrationBuilder.DropTable(
                name: "ExternalReview");

            migrationBuilder.DropTable(
                name: "ExternalSeriesMetadata");
        }
    }
}
