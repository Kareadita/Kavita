using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVolumeCoverMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KPlusOverrides",
                table: "Volume",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "EnableVolumeCoverImage",
                table: "MetadataSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KPlusOverrides",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "EnableVolumeCoverImage",
                table: "MetadataSettings");
        }
    }
}
