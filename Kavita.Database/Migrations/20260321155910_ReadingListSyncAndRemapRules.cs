using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReadingListSyncAndRemapRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadUrl",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncCheckUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShaHash",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePath",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReadingListRemapRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NormalizedCblSeriesName = table.Column<string>(type: "TEXT", nullable: false),
                    CblVolume = table.Column<string>(type: "TEXT", nullable: true),
                    CblNumber = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: true),
                    CblSeriesName = table.Column<string>(type: "TEXT", nullable: false),
                    SeriesNameAtMapping = table.Column<string>(type: "TEXT", nullable: false),
                    IsGlobal = table.Column<bool>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "IX_ReadingListRemapRule_NormalizedCblSeriesName_IsGlobal_AppUserId",
                table: "ReadingListRemapRule",
                columns: new[] { "NormalizedCblSeriesName", "IsGlobal", "AppUserId" });

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

            migrationBuilder.DropColumn(
                name: "DownloadUrl",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "LastSyncCheckUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "ShaHash",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "SourcePath",
                table: "ReadingList");
        }
    }
}
