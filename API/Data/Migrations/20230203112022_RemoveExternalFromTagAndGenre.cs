using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class RemoveExternalFromTagAndGenre : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tag_NormalizedTitle_ExternalTag",
                table: "Tag");

            migrationBuilder.DropIndex(
                name: "IX_Genre_NormalizedTitle_ExternalTag",
                table: "Genre");

            migrationBuilder.DropColumn(
                name: "ExternalTag",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "ExternalTag",
                table: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_NormalizedTitle",
                table: "Tag",
                column: "NormalizedTitle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genre_NormalizedTitle",
                table: "Genre",
                column: "NormalizedTitle",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tag_NormalizedTitle",
                table: "Tag");

            migrationBuilder.DropIndex(
                name: "IX_Genre_NormalizedTitle",
                table: "Genre");

            migrationBuilder.AddColumn<bool>(
                name: "ExternalTag",
                table: "Tag",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalTag",
                table: "Genre",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tag_NormalizedTitle_ExternalTag",
                table: "Tag",
                columns: new[] { "NormalizedTitle", "ExternalTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genre_NormalizedTitle_ExternalTag",
                table: "Genre",
                columns: new[] { "NormalizedTitle", "ExternalTag" },
                unique: true);
        }
    }
}
