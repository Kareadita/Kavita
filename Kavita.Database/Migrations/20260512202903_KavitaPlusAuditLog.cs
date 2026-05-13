using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class KavitaPlusAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KavitaPlusAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KavitaPlusAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLog_Category_CreatedUtc",
                table: "KavitaPlusAuditLogs",
                columns: new[] { "Category", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLog_CreatedUtc",
                table: "KavitaPlusAuditLogs",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLog_SeriesId_CreatedUtc",
                table: "KavitaPlusAuditLogs",
                columns: new[] { "SeriesId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_KavitaPlusAuditLog_SubjectType_SubjectId",
                table: "KavitaPlusAuditLogs",
                columns: new[] { "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KavitaPlusAuditLogs");
        }
    }
}
