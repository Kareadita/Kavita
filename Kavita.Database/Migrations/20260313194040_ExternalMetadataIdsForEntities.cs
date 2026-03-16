using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExternalMetadataIdsForEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AniListId",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineId",
                table: "Volume",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardcoverId",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MalId",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MangaBakaId",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MetronId",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AniListId",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineId",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardcoverId",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MalId",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MangaBakaId",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MetronId",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AniListId",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineId",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardcoverId",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MalId",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MangaBakaId",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MetronId",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AniListId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MalId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MangaBakaId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MetronId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "AniListId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MalId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MangaBakaId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MetronId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "AniListId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "MalId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "MangaBakaId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "MetronId",
                table: "Chapter");
        }
    }
}
