using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleRuleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuleHashSnapshot",
                table: "ScrobbleEvent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransitionRuleKind",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScrobbleRuleHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: true),
                    RuleHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScrobbleEventId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleRuleHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleRuleHistory_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrobbleRuleHistory_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrobbleRuleHistory_ScrobbleEvent_ScrobbleEventId",
                        column: x => x.ScrobbleEventId,
                        principalTable: "ScrobbleEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScrobbleRuleHistory_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleRuleHistory_AppUserId_SeriesId",
                table: "ScrobbleRuleHistory",
                columns: new[] { "AppUserId", "SeriesId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleRuleHistory_ChapterId",
                table: "ScrobbleRuleHistory",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleRuleHistory_ScrobbleEventId",
                table: "ScrobbleRuleHistory",
                column: "ScrobbleEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleRuleHistory_SeriesId",
                table: "ScrobbleRuleHistory",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleRuleHistory_User_Provider_Rule_Series_Chapter",
                table: "ScrobbleRuleHistory",
                columns: new[] { "AppUserId", "Provider", "RuleKind", "SeriesId", "ChapterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrobbleRuleHistory");

            migrationBuilder.DropColumn(
                name: "RuleHashSnapshot",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "TransitionRuleKind",
                table: "ScrobbleEvent");
        }
    }
}
