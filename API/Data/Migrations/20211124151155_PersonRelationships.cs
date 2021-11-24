using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class PersonRelationships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropForeignKey(
                name: "FK_Person_SeriesMetadata_SeriesMetadataId",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Person_ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Person_SeriesMetadataId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "ChapterMetadataId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "SeriesMetadataId",
                table: "Person");

            migrationBuilder.CreateTable(
                name: "ChapterMetadataPerson",
                columns: table => new
                {
                    ChapterMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeopleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterMetadataPerson", x => new { x.ChapterMetadatasId, x.PeopleId });
                    table.ForeignKey(
                        name: "FK_ChapterMetadataPerson_ChapterMetadata_ChapterMetadatasId",
                        column: x => x.ChapterMetadatasId,
                        principalTable: "ChapterMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterMetadataPerson_Person_PeopleId",
                        column: x => x.PeopleId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonSeriesMetadata",
                columns: table => new
                {
                    PeopleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonSeriesMetadata", x => new { x.PeopleId, x.SeriesMetadatasId });
                    table.ForeignKey(
                        name: "FK_PersonSeriesMetadata_Person_PeopleId",
                        column: x => x.PeopleId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonSeriesMetadata_SeriesMetadata_SeriesMetadatasId",
                        column: x => x.SeriesMetadatasId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChapterMetadataPerson_PeopleId",
                table: "ChapterMetadataPerson",
                column: "PeopleId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonSeriesMetadata_SeriesMetadatasId",
                table: "PersonSeriesMetadata",
                column: "SeriesMetadatasId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterMetadataPerson");

            migrationBuilder.DropTable(
                name: "PersonSeriesMetadata");

            migrationBuilder.AddColumn<int>(
                name: "ChapterMetadataId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "RowVersion",
                table: "Person",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<int>(
                name: "SeriesMetadataId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_ChapterMetadataId",
                table: "Person",
                column: "ChapterMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_SeriesMetadataId",
                table: "Person",
                column: "SeriesMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Person_ChapterMetadata_ChapterMetadataId",
                table: "Person",
                column: "ChapterMetadataId",
                principalTable: "ChapterMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Person_SeriesMetadata_SeriesMetadataId",
                table: "Person",
                column: "SeriesMetadataId",
                principalTable: "SeriesMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
