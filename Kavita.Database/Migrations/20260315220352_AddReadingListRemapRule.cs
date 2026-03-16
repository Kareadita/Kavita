using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingListRemapRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadingListRemapRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NormalizedCblSeriesName = table.Column<string>(type: "TEXT", nullable: true),
                    CblVolume = table.Column<string>(type: "TEXT", nullable: true),
                    CblNumber = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeriesNameAtMapping = table.Column<string>(type: "TEXT", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingListRemapRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingListRemapRule_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingListRemapRule_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReadingListRemapRule_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingListRemapRule_Volume_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "Volume",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListRemapRule_AppUserId",
                table: "ReadingListRemapRule",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListRemapRule_ChapterId",
                table: "ReadingListRemapRule",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListRemapRule_NormalizedCblSeriesName_AppUserId",
                table: "ReadingListRemapRule",
                columns: new[] { "NormalizedCblSeriesName", "AppUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListRemapRule_SeriesId",
                table: "ReadingListRemapRule",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingListRemapRule_VolumeId",
                table: "ReadingListRemapRule",
                column: "VolumeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingListRemapRule");
        }
    }
}
