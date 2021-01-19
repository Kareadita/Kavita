using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ReadStatusModifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserProgress_AspNetUsers_AppUserId",
                table: "AppUserProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserProgress_Volume_VolumeId",
                table: "AppUserProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_Volume_AppUserProgress_ProgressId",
                table: "Volume");

            migrationBuilder.DropIndex(
                name: "IX_Volume_ProgressId",
                table: "Volume");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppUserProgress",
                table: "AppUserProgress");

            migrationBuilder.DropIndex(
                name: "IX_AppUserProgress_VolumeId",
                table: "AppUserProgress");

            migrationBuilder.DropColumn(
                name: "ProgressId",
                table: "Volume");

            migrationBuilder.RenameTable(
                name: "AppUserProgress",
                newName: "AppUserProgresses");

            migrationBuilder.RenameIndex(
                name: "IX_AppUserProgress_AppUserId",
                table: "AppUserProgresses",
                newName: "IX_AppUserProgresses_AppUserId");

            migrationBuilder.AlterColumn<int>(
                name: "VolumeId",
                table: "AppUserProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "AppUserProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppUserProgresses",
                table: "AppUserProgresses",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserProgresses_AspNetUsers_AppUserId",
                table: "AppUserProgresses",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserProgresses_AspNetUsers_AppUserId",
                table: "AppUserProgresses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppUserProgresses",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "AppUserProgresses");

            migrationBuilder.RenameTable(
                name: "AppUserProgresses",
                newName: "AppUserProgress");

            migrationBuilder.RenameIndex(
                name: "IX_AppUserProgresses_AppUserId",
                table: "AppUserProgress",
                newName: "IX_AppUserProgress_AppUserId");

            migrationBuilder.AddColumn<int>(
                name: "ProgressId",
                table: "Volume",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "VolumeId",
                table: "AppUserProgress",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppUserProgress",
                table: "AppUserProgress",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Volume_ProgressId",
                table: "Volume",
                column: "ProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProgress_VolumeId",
                table: "AppUserProgress",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserProgress_AspNetUsers_AppUserId",
                table: "AppUserProgress",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserProgress_Volume_VolumeId",
                table: "AppUserProgress",
                column: "VolumeId",
                principalTable: "Volume",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Volume_AppUserProgress_ProgressId",
                table: "Volume",
                column: "ProgressId",
                principalTable: "AppUserProgress",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
