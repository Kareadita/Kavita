using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChapterMetadataLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgeRatingLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CharacterLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ColoristLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CoverArtistLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EditorLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GenresLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ISBNLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImprintLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InkerLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LanguageLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LettererLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LocationLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PencillerLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PublisherLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReleaseDateLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SummaryLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TagsLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TeamLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TitleNameLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TranslatorLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WriterLocked",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRatingLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "CharacterLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "ColoristLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "CoverArtistLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "EditorLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "GenresLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "ISBNLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "ImprintLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "InkerLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "LanguageLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "LettererLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "LocationLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "PencillerLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "PublisherLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "ReleaseDateLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "SummaryLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "TagsLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "TeamLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "TitleNameLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "TranslatorLocked",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "WriterLocked",
                table: "Chapter");
        }
    }
}
