using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AppUserKoboTagTombstone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserKoboTagTombstone",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserKoboTagTombstone", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserKoboTagTombstone_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboTagTombstone_AppUserId_TagId",
                table: "AppUserKoboTagTombstone",
                columns: new[] { "AppUserId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserKoboTagTombstone_TagId",
                table: "AppUserKoboTagTombstone",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserKoboTagTombstone");
        }
    }
}
