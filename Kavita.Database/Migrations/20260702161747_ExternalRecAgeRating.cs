using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExternalRecAgeRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeRating",
                table: "ExternalRecommendation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Update existing data to X18Plus (14) to explicitly opt-out recs from non-admin's so they don't see potentially explicit
            // series
            migrationBuilder.Sql("UPDATE ExternalRecommendation SET AgeRating = 14 WHERE SeriesId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "ExternalRecommendation");
        }
    }
}
