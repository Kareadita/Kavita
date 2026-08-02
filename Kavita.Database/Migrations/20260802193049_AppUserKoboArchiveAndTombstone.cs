using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AppUserKoboArchiveAndTombstone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserKoboArchivedChapter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserKoboArchivedChapter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserKoboArchivedChapter_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserKoboArchivedChapter_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserKoboTombstone",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserKoboTombstone", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserKoboTombstone_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboArchivedChapter_AppUserId_ChapterId",
                table: "AppUserKoboArchivedChapter",
                columns: new[] { "AppUserId", "ChapterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboArchivedChapter_ChapterId",
                table: "AppUserKoboArchivedChapter",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboTombstone_AppUserId_ChapterId",
                table: "AppUserKoboTombstone",
                columns: new[] { "AppUserId", "ChapterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboTombstone_EntitlementId",
                table: "AppUserKoboTombstone",
                column: "EntitlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserKoboArchivedChapter");

            migrationBuilder.DropTable(
                name: "AppUserKoboTombstone");
        }
    }
}
