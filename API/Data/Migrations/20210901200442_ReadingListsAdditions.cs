using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ReadingListsAdditions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingListItem_ReadingList_ReadingListId",
                table: "ReadingListItem");

            migrationBuilder.AlterColumn<int>(
                name: "ReadingListId",
                table: "ReadingListItem",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingListItem_ReadingList_ReadingListId",
                table: "ReadingListItem",
                column: "ReadingListId",
                principalTable: "ReadingList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingListItem_ReadingList_ReadingListId",
                table: "ReadingListItem");

            migrationBuilder.AlterColumn<int>(
                name: "ReadingListId",
                table: "ReadingListItem",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingListItem_ReadingList_ReadingListId",
                table: "ReadingListItem",
                column: "ReadingListId",
                principalTable: "ReadingList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
