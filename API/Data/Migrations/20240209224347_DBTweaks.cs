using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DBTweaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRecommendation_Series_SeriesId",
                table: "ExternalRecommendation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRecommendation_Series_SeriesId",
                table: "ExternalRecommendation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id");
        }
    }
}
