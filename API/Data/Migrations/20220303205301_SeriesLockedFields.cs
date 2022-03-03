using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SeriesLockedFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgeRatingLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CharacterLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ColoristLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CoverArtistLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EditorLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GenresLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InkerLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LanguageLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LettererLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PencillerLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PublicationStatusLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PublisherLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SummaryLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TagsLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TranslatorLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WriterLocked",
                table: "SeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LocalizedNameLocked",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NameLocked",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SortNameLocked",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRatingLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "CharacterLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "ColoristLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "CoverArtistLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "EditorLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "GenresLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "InkerLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "LanguageLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "LettererLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "PencillerLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "PublicationStatusLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "PublisherLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "SummaryLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "TagsLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "TranslatorLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "WriterLocked",
                table: "SeriesMetadata");

            migrationBuilder.DropColumn(
                name: "LocalizedNameLocked",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "NameLocked",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SortNameLocked",
                table: "Series");
        }
    }
}
