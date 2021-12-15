using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SeriesIncludes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppUserRating_SeriesId",
                table: "AppUserRating",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgresses_SeriesId",
                table: "AppUserProgresses",
                column: "SeriesId");

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

            migrationBuilder.DropIndex(
                name: "IX_AppUserRating_SeriesId",
                table: "AppUserRating");

            migrationBuilder.DropIndex(
                name: "IX_AppUserProgresses_SeriesId",
                table: "AppUserProgresses");
        }
    }
}
