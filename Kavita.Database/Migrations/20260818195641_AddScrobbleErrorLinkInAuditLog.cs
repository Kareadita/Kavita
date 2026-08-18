using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScrobbleErrorLinkInAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScrobbleErrorId",
                table: "KavitaPlusAuditLogs",
                type: "INTEGER",
                nullable: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KavitaPlusAuditLogs_ScrobbleError_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_KavitaPlusAuditLogs_ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropColumn(
                name: "ScrobbleErrorId",
                table: "KavitaPlusAuditLogs");
        }
    }
}
