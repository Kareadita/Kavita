using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class ChapterMetadataOptimization : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapter_Genre_GenreId",
                table: "Chapter");

            migrationBuilder.DropIndex(
                name: "IX_Chapter_GenreId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "GenreId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "FullscreenMode",
                table: "AppUserPreferences");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChapterGenre",
                columns: table => new
                {
                    ChaptersId = table.Column<int>(type: "INTEGER", nullable: false),
                    GenresId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterGenre", x => new { x.ChaptersId, x.GenresId });
                    table.ForeignKey(
                        name: "FK_ChapterGenre_Chapter_ChaptersId",
                        column: x => x.ChaptersId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterGenre_Genre_GenresId",
                        column: x => x.GenresId,
                        principalTable: "Genre",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChapterGenre_GenresId",
                table: "ChapterGenre",
                column: "GenresId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterGenre");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Chapter");

            migrationBuilder.AddColumn<int>(
                name: "GenreId",
                table: "Chapter",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FullscreenMode",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Chapter_GenreId",
                table: "Chapter",
                column: "GenreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chapter_Genre_GenreId",
                table: "Chapter",
                column: "GenreId",
                principalTable: "Genre",
                principalColumn: "Id");
        }
    }
}
