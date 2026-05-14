using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class KavitaPlusAuditLog_AddUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "KavitaPlusAuditLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLog_UserId",
                table: "KavitaPlusAuditLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_KavitaPlusAuditLogs_AspNetUsers_UserId",
                table: "KavitaPlusAuditLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KavitaPlusAuditLogs_AspNetUsers_UserId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_KavitaPlusAuditLog_UserId",
                table: "KavitaPlusAuditLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "KavitaPlusAuditLogs");
        }
    }
}
