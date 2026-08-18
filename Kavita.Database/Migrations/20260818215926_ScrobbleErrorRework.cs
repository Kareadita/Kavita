using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleErrorRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "ScrobbleError",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScrobbleErrorId",
                table: "KavitaPlusAuditLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleError_ChapterId",
                table: "ScrobbleError",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLogs_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs",
                column: "ScrobbleErrorId");

            migrationBuilder.AddForeignKey(
                name: "FK_KavitaPlusAuditLogs_ScrobbleError_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs",
                column: "ScrobbleErrorId",
                principalTable: "ScrobbleError",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KavitaPlusAuditLogs_ScrobbleError_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleError_Chapter_ChapterId",
                table: "ScrobbleError");

            migrationBuilder.DropIndex(
                name: "IX_ScrobbleError_ChapterId",
                table: "ScrobbleError");

            migrationBuilder.DropIndex(
                name: "IX_KavitaPlusAuditLogs_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "ScrobbleError");

            migrationBuilder.DropColumn(
                name: "ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");
        }
    }
}
