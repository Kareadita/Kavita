using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class SeriesRelationChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation");

            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_TargetSeriesId",
                table: "SeriesRelation");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_TargetSeriesId",
                table: "SeriesRelation",
                column: "TargetSeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation");

            migrationBuilder.DropForeignKey(
                name: "FK_SeriesRelation_Series_TargetSeriesId",
                table: "SeriesRelation");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_SeriesId",
                table: "SeriesRelation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesRelation_Series_TargetSeriesId",
                table: "SeriesRelation",
                column: "TargetSeriesId",
                principalTable: "Series",
                principalColumn: "Id");
        }
    }
}
