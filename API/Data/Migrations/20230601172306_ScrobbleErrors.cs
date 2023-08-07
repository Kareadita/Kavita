using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrobbleError",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrobbleEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrobbleEventId1 = table.Column<long>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleError", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleError_ScrobbleEvent_ScrobbleEventId1",
                        column: x => x.ScrobbleEventId1,
                        principalTable: "ScrobbleEvent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScrobbleError_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleError_ScrobbleEventId1",
                table: "ScrobbleError",
                column: "ScrobbleEventId1");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleError_SeriesId",
                table: "ScrobbleError",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrobbleError");
        }
    }
}
