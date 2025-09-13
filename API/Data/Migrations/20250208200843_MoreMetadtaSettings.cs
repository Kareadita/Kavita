using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoreMetadtaSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KavitaPlusConnection",
                table: "SeriesMetadataPeople",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OrderWeight",
                table: "SeriesMetadataPeople",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCoverImage",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Overrides",
                table: "MetadataSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KavitaPlusConnection",
                table: "SeriesMetadataPeople");

            migrationBuilder.DropColumn(
                name: "OrderWeight",
                table: "SeriesMetadataPeople");

            migrationBuilder.DropColumn(
                name: "EnableCoverImage",
                table: "MetadataSettings");

            migrationBuilder.DropColumn(
                name: "Overrides",
                table: "MetadataSettings");
        }
    }
}
