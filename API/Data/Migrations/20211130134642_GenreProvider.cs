using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class GenreProvider : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropTable(
                name: "ChapterMetadata");

            migrationBuilder.DropIndex(
                name: "IX_Person_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "ChapterMetadataId",
                table: "Person");

            migrationBuilder.AddColumn<bool>(
                name: "ExternalTag",
                table: "Genre",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalTag",
                table: "Genre");

            migrationBuilder.AddColumn<int>(
                name: "ChapterMetadataId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChapterMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    StoryArc = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChapterMetadata_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Person_ChapterMetadataId",
                table: "Person",
                column: "ChapterMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterMetadata_ChapterId",
                table: "ChapterMetadata",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person",
                column: "ChapterMetadataId",
                principalTable: "ChapterMetadata",
                principalColumn: "Id");
        }
    }
}
