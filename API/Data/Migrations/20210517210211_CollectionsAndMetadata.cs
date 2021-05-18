using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class CollectionsAndMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeriesMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Publisher = table.Column<string>(type: "TEXT", nullable: true),
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
                name: "Genre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                    SeriesMetadataId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Genre_SeriesMetadata_SeriesMetadataId",
                        column: x => x.SeriesMetadataId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                    SeriesMetadataId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Person_SeriesMetadata_SeriesMetadataId",
                        column: x => x.SeriesMetadataId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                    SeriesMetadataId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tag_SeriesMetadata_SeriesMetadataId",
                        column: x => x.SeriesMetadataId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Genre_SeriesMetadataId",
                table: "Genre",
                column: "SeriesMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_SeriesMetadataId",
                table: "Person",
                column: "SeriesMetadataId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Tag_SeriesMetadataId",
                table: "Tag",
                column: "SeriesMetadataId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Genre");

            migrationBuilder.DropTable(
                name: "Person");

            migrationBuilder.DropTable(
                name: "Tag");

            migrationBuilder.DropTable(
                name: "SeriesMetadata");
        }
    }
}
