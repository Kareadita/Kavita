using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class GenreTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Genre_NormalizedName",
                table: "Genre");

            migrationBuilder.RenameColumn(
                name: "NormalizedName",
                table: "Genre",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Genre",
                newName: "NormalizedTitle");

            migrationBuilder.AddColumn<int>(
                name: "GenreId",
                table: "Chapter",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genre_NormalizedTitle_ExternalTag",
                table: "Genre",
                columns: new[] { "NormalizedTitle", "ExternalTag" },
                unique: true);

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapter_Genre_GenreId",
                table: "Chapter");

            migrationBuilder.DropIndex(
                name: "IX_Genre_NormalizedTitle_ExternalTag",
                table: "Genre");

            migrationBuilder.DropIndex(
                name: "IX_Chapter_GenreId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "GenreId",
                table: "Chapter");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Genre",
                newName: "NormalizedName");

            migrationBuilder.RenameColumn(
                name: "NormalizedTitle",
                table: "Genre",
                newName: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Genre_NormalizedName",
                table: "Genre",
                column: "NormalizedName",
                unique: true);
        }
    }
}
