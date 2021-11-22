using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class Persons : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesMetadataId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_SeriesMetadataId",
                table: "Person",
                column: "SeriesMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Person_SeriesMetadata_SeriesMetadataId",
                table: "Person",
                column: "SeriesMetadataId",
                principalTable: "SeriesMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Person_SeriesMetadata_SeriesMetadataId",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Person_SeriesMetadataId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "SeriesMetadataId",
                table: "Person");
        }
    }
}
