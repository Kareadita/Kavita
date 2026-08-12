using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class KPlusTagAndAgeRatingFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableAgeRating",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAgeRatingMappings",
                table: "MetadataSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilterAboveWeight",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableAgeRating",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "ExternalAgeRatingMappings",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "FilterAboveWeight",
                table: "MetadataSettings");
        }
    }
}
