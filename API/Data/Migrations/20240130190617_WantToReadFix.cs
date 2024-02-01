using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class WantToReadFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Series_AspNetUsers_AppUserId",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_AppUserId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Series");

            migrationBuilder.CreateTable(
                name: "AppUserWantToRead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserWantToRead", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserWantToRead_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserWantToRead_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualMigrationHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductVersion = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    RanAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualMigrationHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserWantToRead_AppUserId",
                table: "AppUserWantToRead",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserWantToRead_SeriesId",
                table: "AppUserWantToRead",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserWantToRead");

            migrationBuilder.DropTable(
                name: "ManualMigrationHistory");

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_AppUserId",
                table: "Series",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Series_AspNetUsers_AppUserId",
                table: "Series",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
