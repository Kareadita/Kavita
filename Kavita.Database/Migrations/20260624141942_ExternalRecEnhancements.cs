using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExternalRecEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HardCoverId",
                table: "ExternalRecommendation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MangaBakaId",
                table: "ExternalRecommendation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetadataProvider",
                table: "ExternalRecommendation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecommendationSource",
                table: "ExternalRecommendation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardCoverId",
                table: "ExternalRecommendation");

            migrationBuilder.DropColumn(
                name: "MangaBakaId",
                table: "ExternalRecommendation");

            migrationBuilder.DropColumn(
                name: "MetadataProvider",
                table: "ExternalRecommendation");

            migrationBuilder.DropColumn(
                name: "RecommendationSource",
                table: "ExternalRecommendation");
        }
    }
}
