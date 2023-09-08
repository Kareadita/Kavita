using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DashboardStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserDashboardStream",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IsProvided = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 4),
                    Visible = table.Column<bool>(type: "INTEGER", nullable: false),
                    SmartFilterId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserDashboardStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserDashboardStream_AppUserSmartFilter_SmartFilterId",
                        column: x => x.SmartFilterId,
                        principalTable: "AppUserSmartFilter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppUserDashboardStream_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserDashboardStream_AppUserId",
                table: "AppUserDashboardStream",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserDashboardStream_SmartFilterId",
                table: "AppUserDashboardStream",
                column: "SmartFilterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserDashboardStream_Visible",
                table: "AppUserDashboardStream",
                column: "Visible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserDashboardStream");
        }
    }
}
