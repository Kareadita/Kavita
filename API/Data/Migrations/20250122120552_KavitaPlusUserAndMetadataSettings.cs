using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class KavitaPlusUserAndMetadataSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AniListScrobblingEnabled",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WantToReadSync",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MetadataSettings",
                columns: table => new
                {
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableSummary = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePublicationStatus = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableRelationships = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePeople = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableStartDate = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "AniListScrobblingEnabled",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "WantToReadSync",
                table: "AppUserPreferences");
        }
    }
}
