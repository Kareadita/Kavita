using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoverPrimaryColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Volume",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Volume",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Library",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Library",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "AppUserCollection",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "AppUserCollection",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "AppUserCollection");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "AppUserCollection");
        }
    }
}
