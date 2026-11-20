using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackDeviceIdForKoboSyncPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RemovedChapterIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "ReadingListsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KoboDeviceId",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CollectionsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChapterIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeviceId",
                table: "KoboSyncPoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KoboSyncPoints_DeviceId",
                table: "KoboSyncPoints",
                column: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_KoboSyncPoints_Device_DeviceId",
                table: "KoboSyncPoints",
                column: "DeviceId",
                principalTable: "Device",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KoboSyncPoints_Device_DeviceId",
                table: "KoboSyncPoints");

            migrationBuilder.DropIndex(
                name: "IX_KoboSyncPoints_DeviceId",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "KoboSyncPoints");

            migrationBuilder.AlterColumn<string>(
                name: "RemovedChapterIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "ReadingListsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "KoboDeviceId",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "CollectionsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ChapterIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
