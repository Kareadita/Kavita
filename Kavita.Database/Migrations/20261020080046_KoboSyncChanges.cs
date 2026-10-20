using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class KoboSyncChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KoboSpan",
                table: "AppUserProgresses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"AllLibraries\":false,\"SyncLibraries\":[],\"SyncCollection\":0}");

            migrationBuilder.AddColumn<string>(
                name: "KoboId",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KoboSyncPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BookSyncTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BooksSyncedUntilId = table.Column<int>(type: "INTEGER", nullable: false),
                    BooksSyncedUntilIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressSyncTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgressSyncedUntilId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressSyncedUntilIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KoboSyncPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KoboSyncPoints_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KoboSyncPoints_UserId",
                table: "KoboSyncPoints",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "KoboSpan",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "KoboId",
                table: "AppUserAnnotation");
        }
    }
}
