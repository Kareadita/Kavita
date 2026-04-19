using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEpubFontFamilyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Family",
                table: "EpubFont",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "EpubFont",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weight",
                table: "EpubFont",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Family",
                table: "EpubFont");

            migrationBuilder.DropColumn(
                name: "Style",
                table: "EpubFont");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "EpubFont");
        }
    }
}
