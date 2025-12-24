using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadingSessionFormatAndIndecies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean up orphaned records before adding FK constraints
            migrationBuilder.Sql(@"
                DELETE FROM AppUserReadingSessionActivityData
                WHERE LibraryId NOT IN (SELECT Id FROM Library)
                   OR VolumeId NOT IN (SELECT Id FROM Volume)
                   OR LibraryId = 0
                   OR VolumeId = 0;
            ");

            migrationBuilder.AddColumn<int>(
                name: "Format",
                table: "AppUserReadingSessionActivityData",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityData_StartTimeUtc_LibraryId",
                table: "AppUserReadingSessionActivityData",
                columns: new[] { "StartTimeUtc", "LibraryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSessionActivityData_LibraryId",
                table: "AppUserReadingSessionActivityData",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSessionActivityData_VolumeId",
                table: "AppUserReadingSessionActivityData",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserReadingSessionActivityData_Library_LibraryId",
                table: "AppUserReadingSessionActivityData",
                column: "LibraryId",
                principalTable: "Library",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserReadingSessionActivityData_Volume_VolumeId",
                table: "AppUserReadingSessionActivityData",
                column: "VolumeId",
                principalTable: "Volume",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserReadingSessionActivityData_Library_LibraryId",
                table: "AppUserReadingSessionActivityData");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserReadingSessionActivityData_Volume_VolumeId",
                table: "AppUserReadingSessionActivityData");

            migrationBuilder.DropIndex(
                name: "IX_ActivityData_StartTimeUtc_LibraryId",
                table: "AppUserReadingSessionActivityData");

            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingSessionActivityData_LibraryId",
                table: "AppUserReadingSessionActivityData");

            migrationBuilder.DropIndex(
                name: "IX_AppUserReadingSessionActivityData_VolumeId",
                table: "AppUserReadingSessionActivityData");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "AppUserReadingSessionActivityData");
        }
    }
}
