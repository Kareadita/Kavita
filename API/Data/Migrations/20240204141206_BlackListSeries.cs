using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class BlackListSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastUpdatedUtc",
                table: "ExternalSeriesMetadata",
                newName: "ValidUntilUtc");

            migrationBuilder.CreateTable(
                name: "SeriesBlacklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastChecked = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesBlacklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesBlacklist_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesBlacklist_SeriesId",
                table: "SeriesBlacklist",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeriesBlacklist");

            migrationBuilder.RenameColumn(
                name: "ValidUntilUtc",
                table: "ExternalSeriesMetadata",
                newName: "LastUpdatedUtc");
        }
    }
}
