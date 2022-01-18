using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class filteringChanges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "SeriesMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTitle = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalTag = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChapterTag",
                columns: table => new
                {
                    ChaptersId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterTag", x => new { x.ChaptersId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ChapterTag_Chapter_ChaptersId",
                        column: x => x.ChaptersId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterTag_Tag_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesMetadataTag",
                columns: table => new
                {
                    SeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesMetadataTag", x => new { x.SeriesMetadatasId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_SeriesMetadataTag_SeriesMetadata_SeriesMetadatasId",
                        column: x => x.SeriesMetadatasId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesMetadataTag_Tag_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRating_SeriesId",
                table: "AppUserRating",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgresses_SeriesId",
                table: "AppUserProgresses",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterTag_TagsId",
                table: "ChapterTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadataTag_TagsId",
                table: "SeriesMetadataTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_NormalizedTitle_ExternalTag",
                table: "Tag",
                columns: new[] { "NormalizedTitle", "ExternalTag" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserProgresses_Series_SeriesId",
                table: "AppUserProgresses",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserRating_Series_SeriesId",
                table: "AppUserRating",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserProgresses_Series_SeriesId",
                table: "AppUserProgresses");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserRating_Series_SeriesId",
                table: "AppUserRating");

            migrationBuilder.DropTable(
                name: "ChapterTag");

            migrationBuilder.DropTable(
                name: "SeriesMetadataTag");

            migrationBuilder.DropTable(
                name: "Tag");

            migrationBuilder.DropIndex(
                name: "IX_AppUserRating_SeriesId",
                table: "AppUserRating");

            migrationBuilder.DropIndex(
                name: "IX_AppUserProgresses_SeriesId",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "SeriesMetadata");
        }
    }
}
