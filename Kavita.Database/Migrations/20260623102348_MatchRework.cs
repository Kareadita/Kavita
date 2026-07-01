using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class MatchRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStandAlone",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MetadataProvider",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: MetadataProvider.Mangabaka);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStandAlone",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MetadataProvider",
                table: "Library");
        }
    }
}
