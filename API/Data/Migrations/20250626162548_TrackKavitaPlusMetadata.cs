using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackKavitaPlusMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KPlusOverrides",
                table: "SeriesMetadata",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "KPlusOverrides",
                table: "Chapter",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KPlusOverrides",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "KPlusOverrides",
                table: "Chapter");
        }
    }
}
