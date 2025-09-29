using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SocialAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShareAnnotations",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialIncludeUnknowns",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialLibraries",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SocialMaxAgeRating",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<bool>(
                name: "ViewOtherAnnotations",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Likes",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShareAnnotations",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "SocialIncludeUnknowns",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "SocialLibraries",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "SocialMaxAgeRating",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "ViewOtherAnnotations",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "AppUserAnnotation");
        }
    }
}
