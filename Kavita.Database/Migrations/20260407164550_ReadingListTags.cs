using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReadingListTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalItemsAtImport",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReadingListTag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTitle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingListTag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadingListReadingListTag",
                columns: table => new
                {
                    ReadingListsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingListReadingListTag", x => new { x.ReadingListsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ReadingListReadingListTag_ReadingListTag_TagsId",
                        column: x => x.TagsId,
                        principalTable: "ReadingListTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingListReadingListTag_ReadingList_ReadingListsId",
                        column: x => x.ReadingListsId,
                        principalTable: "ReadingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListReadingListTag_TagsId",
                table: "ReadingListReadingListTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListTag_NormalizedTitle",
                table: "ReadingListTag",
                column: "NormalizedTitle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingListReadingListTag");

            migrationBuilder.DropTable(
                name: "ReadingListTag");

            migrationBuilder.DropColumn(
                name: "TotalItemsAtImport",
                table: "ReadingList");
        }
    }
}
