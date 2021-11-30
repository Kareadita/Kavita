using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class RemoveChapterMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterMetadataPerson");

            migrationBuilder.DropIndex(
                name: "IX_ChapterMetadata_ChapterId",
                table: "ChapterMetadata");

            migrationBuilder.AddColumn<int>(
                name: "ChapterMetadataId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleName",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChapterPerson",
                columns: table => new
                {
                    ChapterMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeopleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterPerson", x => new { x.ChapterMetadatasId, x.PeopleId });
                    table.ForeignKey(
                        name: "FK_ChapterPerson_Chapter_ChapterMetadatasId",
                        column: x => x.ChapterMetadatasId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterPerson_Person_PeopleId",
                        column: x => x.PeopleId,
                        principalTable: "Person",
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

            migrationBuilder.CreateIndex(
                name: "IX_ChapterPerson_PeopleId",
                table: "ChapterPerson",
                column: "PeopleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person",
                column: "ChapterMetadataId",
                principalTable: "ChapterMetadata",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropTable(
                name: "ChapterPerson");

            migrationBuilder.DropIndex(
                name: "IX_Person_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_ChapterMetadata_ChapterId",
                table: "ChapterMetadata");

            migrationBuilder.DropColumn(
                name: "ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "TitleName",
                table: "Chapter");

            migrationBuilder.CreateTable(
                name: "ChapterMetadataPerson",
                columns: table => new
                {
                    ChapterMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeopleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterMetadataPerson", x => new { x.ChapterMetadatasId, x.PeopleId });
                    table.ForeignKey(
                        name: "FK_ChapterMetadataPerson_ChapterMetadata_ChapterMetadatasId",
                        column: x => x.ChapterMetadatasId,
                        principalTable: "ChapterMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterMetadataPerson_Person_PeopleId",
                        column: x => x.PeopleId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChapterMetadata_ChapterId",
                table: "ChapterMetadata",
                column: "ChapterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChapterMetadataPerson_PeopleId",
                table: "ChapterMetadataPerson",
                column: "PeopleId");
        }
    }
}
