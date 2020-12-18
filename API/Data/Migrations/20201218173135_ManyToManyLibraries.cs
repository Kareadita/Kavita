using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ManyToManyLibraries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolderPath_Library_LibraryId",
                table: "FolderPath");

            migrationBuilder.DropForeignKey(
                name: "FK_Library_Users_AppUserId",
                table: "Library");

            migrationBuilder.DropIndex(
                name: "IX_Library_AppUserId",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Library");

            migrationBuilder.AlterColumn<int>(
                name: "LibraryId",
                table: "FolderPath",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AppUserLibrary",
                columns: table => new
                {
                    AppUsersId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibrariesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserLibrary", x => new { x.AppUsersId, x.LibrariesId });
                    table.ForeignKey(
                        name: "FK_AppUserLibrary_Library_LibrariesId",
                        column: x => x.LibrariesId,
                        principalTable: "Library",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserLibrary_Users_AppUsersId",
                        column: x => x.AppUsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserLibrary_LibrariesId",
                table: "AppUserLibrary",
                column: "LibrariesId");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPath_Library_LibraryId",
                table: "FolderPath",
                column: "LibraryId",
                principalTable: "Library",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolderPath_Library_LibraryId",
                table: "FolderPath");

            migrationBuilder.DropTable(
                name: "AppUserLibrary");

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "LibraryId",
                table: "FolderPath",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_Library_AppUserId",
                table: "Library",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPath_Library_LibraryId",
                table: "FolderPath",
                column: "LibraryId",
                principalTable: "Library",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Library_Users_AppUserId",
                table: "Library",
                column: "AppUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
