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
                name: "AllowMetadataMatching",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EnableSummary = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePublicationStatus = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableRelationships = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePeople = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableStartDate = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableLocalizedName = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableGenres = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableTags = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstLastPeopleNaming = table.Column<bool>(type: "INTEGER", nullable: false),
                    AgeRatingMappings = table.Column<string>(type: "TEXT", nullable: true),
                    Blacklist = table.Column<string>(type: "TEXT", nullable: true),
                    Whitelist = table.Column<string>(type: "TEXT", nullable: true),
                    PersonRoles = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataFieldMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceValue = table.Column<string>(type: "TEXT", nullable: true),
                    DestinationValue = table.Column<string>(type: "TEXT", nullable: true),
                    ExcludeFromSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataFieldMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataFieldMapping_MetadataSettings_MetadataSettingsId",
                        column: x => x.MetadataSettingsId,
                        principalTable: "MetadataSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataFieldMapping_MetadataSettingsId",
                table: "MetadataFieldMapping",
                column: "MetadataSettingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataFieldMapping");

            migrationBuilder.DropTable(
                name: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "AllowMetadataMatching",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "AniListScrobblingEnabled",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "WantToReadSync",
                table: "AppUserPreferences");
        }
    }
}
