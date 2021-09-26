using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ReadingListsChanges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReadingListItem_SeriesId_VolumeId_ChapterId_LibraryId",
                table: "ReadingListItem");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "ReadingListItem");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListItem_SeriesId",
                table: "ReadingListItem",
                column: "SeriesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReadingListItem_SeriesId",
                table: "ReadingListItem");

            migrationBuilder.AddColumn<int>(
                name: "LibraryId",
                table: "ReadingListItem",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListItem_SeriesId_VolumeId_ChapterId_LibraryId",
                table: "ReadingListItem",
                columns: new[] { "SeriesId", "VolumeId", "ChapterId", "LibraryId" },
                unique: true);
        }
    }
}
