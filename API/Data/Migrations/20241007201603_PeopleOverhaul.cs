using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PeopleOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterPerson");

            migrationBuilder.DropTable(
                name: "PersonSeriesMetadata");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Person",
                newName: "MetronId");

            // migrationBuilder.AddColumn<int>(
            //     name: "AniListId",
            //     table: "Person",
            //     type: "INTEGER",
            //     nullable: false,
            //     defaultValue: 0);
            //
            // migrationBuilder.AddColumn<string>(
            //     name: "Asin",
            //     table: "Person",
            //     type: "TEXT",
            //     nullable: true);

            // migrationBuilder.AddColumn<string>(
            //     name: "CoverImage",
            //     table: "Person",
            //     type: "TEXT",
            //     nullable: true);
            //
            // migrationBuilder.AddColumn<bool>(
            //     name: "CoverImageLocked",
            //     table: "Person",
            //     type: "INTEGER",
            //     nullable: false,
            //     defaultValue: false);

            // migrationBuilder.AddColumn<string>(
            //     name: "Description",
            //     table: "Person",
            //     type: "TEXT",
            //     nullable: true);

            // migrationBuilder.AddColumn<string>(
            //     name: "HardcoverId",
            //     table: "Person",
            //     type: "TEXT",
            //     nullable: true);
            //
            // migrationBuilder.AddColumn<long>(
            //     name: "MalId",
            //     table: "Person",
            //     type: "INTEGER",
            //     nullable: false,
            //     defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Person",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChapterPeople",
                columns: table => new
                {
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterPeople", x => new { x.ChapterId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_ChapterPeople_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterPeople_Person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesMetadataPeople",
                columns: table => new
                {
                    SeriesMetadataId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesMetadataPeople", x => new { x.SeriesMetadataId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_SeriesMetadataPeople_Person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesMetadataPeople_SeriesMetadata_SeriesMetadataId",
                        column: x => x.SeriesMetadataId,
                        principalTable: "SeriesMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChapterPeople_PersonId",
                table: "ChapterPeople",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadataPeople_PersonId",
                table: "SeriesMetadataPeople",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterPeople");

            migrationBuilder.DropTable(
                name: "SeriesMetadataPeople");

            // migrationBuilder.DropColumn(
            //     name: "AniListId",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "Asin",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "CoverImage",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "CoverImageLocked",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "Description",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "HardcoverId",
            //     table: "Person");
            //
            // migrationBuilder.DropColumn(
            //     name: "MalId",
            //     table: "Person");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Person");

            migrationBuilder.RenameColumn(
                name: "MetronId",
                table: "Person",
                newName: "Role");

            migrationBuilder.CreateTable(
                name: "ChapterPerson",
                columns: table => new
                {
                    ChapterMetadatasId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeopleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterPerson", x => new { x.ChapterMetadatasId, x.PeopleId });
                    table.ForeignKey(
                        name: "FK_ChapterPerson_Chapter_ChapterMetadatasId",
                        column: x => x.ChapterMetadatasId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterPerson_Person_PeopleId",
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
                name: "IX_ChapterPerson_PeopleId",
                table: "ChapterPerson",
                column: "PeopleId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonSeriesMetadata_SeriesMetadatasId",
                table: "PersonSeriesMetadata",
                column: "SeriesMetadatasId");
        }
    }
}
