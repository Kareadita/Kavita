using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwipeThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookReaderDistanceThreshold",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookReaderScrollThreshold",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "BookReaderSpeedThreshold",
                table: "AppUserPreferences",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookReaderDistanceThreshold",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "BookReaderScrollThreshold",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "BookReaderSpeedThreshold",
                table: "AppUserPreferences");
        }
    }
}
