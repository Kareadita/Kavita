using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SideNavStreamAndExternalSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserExternalSource",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Host = table.Column<string>(type: "TEXT", nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserExternalSource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserExternalSource_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserSideNavStream",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IsProvided = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalSourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    Visible = table.Column<bool>(type: "INTEGER", nullable: false),
                    SmartFilterId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserSideNavStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserSideNavStream_AppUserSmartFilter_SmartFilterId",
                        column: x => x.SmartFilterId,
                        principalTable: "AppUserSmartFilter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppUserSideNavStream_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserExternalSource_AppUserId",
                table: "AppUserExternalSource",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserSideNavStream_AppUserId",
                table: "AppUserSideNavStream",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserSideNavStream_SmartFilterId",
                table: "AppUserSideNavStream",
                column: "SmartFilterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserSideNavStream_Visible",
                table: "AppUserSideNavStream",
                column: "Visible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserExternalSource");

            migrationBuilder.DropTable(
                name: "AppUserSideNavStream");
        }
    }
}
