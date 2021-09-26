using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ReadingListsExtraRealationships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ReadingListItem_ChapterId",
                table: "ReadingListItem",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListItem_VolumeId",
                table: "ReadingListItem",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingListItem_Chapter_ChapterId",
                table: "ReadingListItem",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingListItem_Series_SeriesId",
                table: "ReadingListItem",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingListItem_Volume_VolumeId",
                table: "ReadingListItem",
                column: "VolumeId",
                principalTable: "Volume",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingListItem_Chapter_ChapterId",
                table: "ReadingListItem");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingListItem_Series_SeriesId",
                table: "ReadingListItem");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingListItem_Volume_VolumeId",
                table: "ReadingListItem");

            migrationBuilder.DropIndex(
                name: "IX_ReadingListItem_ChapterId",
                table: "ReadingListItem");

            migrationBuilder.DropIndex(
                name: "IX_ReadingListItem_VolumeId",
                table: "ReadingListItem");
        }
    }
}
