using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class WantToReadList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_AppUserId",
                table: "Series",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Series_AspNetUsers_AppUserId",
                table: "Series",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Series_AspNetUsers_AppUserId",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_AppUserId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Series");
        }
    }
}
