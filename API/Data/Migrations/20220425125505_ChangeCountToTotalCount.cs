using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class ChangeCountToTotalCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation");

            migrationBuilder.RenameColumn(
                name: "Count",
                table: "SeriesMetadata",
                newName: "TotalCount");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation");

            migrationBuilder.RenameColumn(
                name: "TotalCount",
                table: "SeriesMetadata",
                newName: "Count");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
