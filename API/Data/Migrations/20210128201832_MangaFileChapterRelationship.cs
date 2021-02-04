using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class MangaFileChapterRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MangaFile_Chapter_ChapterId",
                table: "MangaFile");

            migrationBuilder.DropForeignKey(
                name: "FK_MangaFile_Volume_VolumeId",
                table: "MangaFile");

            migrationBuilder.DropIndex(
                name: "IX_MangaFile_VolumeId",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "Chapter",
                table: "MangaFile");

            migrationBuilder.DropColumn(
                name: "VolumeId",
                table: "MangaFile");

            migrationBuilder.AlterColumn<int>(
                name: "ChapterId",
                table: "MangaFile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MangaFile_Chapter_ChapterId",
                table: "MangaFile",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MangaFile_Chapter_ChapterId",
                table: "MangaFile");

            migrationBuilder.AlterColumn<int>(
                name: "ChapterId",
                table: "MangaFile",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "Chapter",
                table: "MangaFile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VolumeId",
                table: "MangaFile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MangaFile_VolumeId",
                table: "MangaFile",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MangaFile_Chapter_ChapterId",
                table: "MangaFile",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MangaFile_Volume_VolumeId",
                table: "MangaFile",
                column: "VolumeId",
                principalTable: "Volume",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
