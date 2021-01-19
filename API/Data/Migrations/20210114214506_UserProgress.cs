using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class UserProgress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressId",
                table: "Volume",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppUserProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PagesRead = table.Column<int>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserProgress_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserProgress_Volume_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "Volume",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Volume_ProgressId",
                table: "Volume",
                column: "ProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgress_AppUserId",
                table: "AppUserProgress",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgress_VolumeId",
                table: "AppUserProgress",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Volume_AppUserProgress_ProgressId",
                table: "Volume",
                column: "ProgressId",
                principalTable: "AppUserProgress",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Volume_AppUserProgress_ProgressId",
                table: "Volume");

            migrationBuilder.DropTable(
                name: "AppUserProgress");

            migrationBuilder.DropIndex(
                name: "IX_Volume_ProgressId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "ProgressId",
                table: "Volume");
        }
    }
}
