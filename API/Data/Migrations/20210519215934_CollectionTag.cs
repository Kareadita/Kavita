using API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class CollectionTag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionTag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Promoted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeriesMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesMetadata_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTagSeriesMetadata",
                columns: table => new
                {
                    CollectionTagsId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesMetadatasId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTagSeriesMetadata", x => new { x.CollectionTagsId, x.SeriesMetadatasId });
                    table.ForeignKey(
                        name: "FK_CollectionTagSeriesMetadata_CollectionTag_CollectionTagsId",
                        column: x => x.CollectionTagsId,
                        principalTable: "CollectionTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionTagSeriesMetadata_SeriesMetadata_SeriesMetadatasId",
                        column: x => x.SeriesMetadatasId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTag_Id_Promoted",
                table: "CollectionTag",
                columns: new[] { "Id", "Promoted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTagSeriesMetadata_SeriesMetadatasId",
                table: "CollectionTagSeriesMetadata",
                column: "SeriesMetadatasId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadata_Id_SeriesId",
                table: "SeriesMetadata",
                columns: new[] { "Id", "SeriesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadata_SeriesId",
                table: "SeriesMetadata",
                column: "SeriesId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionTagSeriesMetadata");

            migrationBuilder.DropTable(
                name: "CollectionTag");

            migrationBuilder.DropTable(
                name: "SeriesMetadata");
        }
    }
}
