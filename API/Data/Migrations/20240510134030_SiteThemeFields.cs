using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SiteThemeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibleVersion",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubPath",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviewUrls",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShaHash",
                table: "SiteTheme",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Author",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "CompatibleVersion",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "GitHubPath",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "PreviewUrls",
                table: "SiteTheme");

            migrationBuilder.DropColumn(
                name: "ShaHash",
                table: "SiteTheme");
        }
    }
}
