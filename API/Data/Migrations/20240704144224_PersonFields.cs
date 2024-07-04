using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AniListId",
                table: "Person",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Asin",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CoverImageLocked",
                table: "Person",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HardcoverId",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MalId",
                table: "Person",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AniListId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "Asin",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "CoverImageLocked",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "MalId",
                table: "Person");
        }
    }
}
