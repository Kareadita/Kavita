using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardcoverId",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBackFill",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MangabakaId",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Progress",
                table: "ScrobbleEvent",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReadStatus",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScrobbleProvider",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HardcoverId",
                table: "ExternalSeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MangabakaId",
                table: "ExternalSeriesMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ScrobbleProviders",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleEvent_ChapterId",
                table: "ScrobbleEvent",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleEvent_Chapter_ChapterId",
                table: "ScrobbleEvent");

            migrationBuilder.DropIndex(
                name: "IX_ScrobbleEvent_ChapterId",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "IsBackFill",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "MangabakaId",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "ReadStatus",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "ScrobbleProvider",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "HardcoverId",
                table: "ExternalSeriesMetadata");

            migrationBuilder.DropColumn(
                name: "MangabakaId",
                table: "ExternalSeriesMetadata");

            migrationBuilder.DropColumn(
                name: "ScrobbleProviders",
                table: "AspNetUsers");
        }
    }
}
