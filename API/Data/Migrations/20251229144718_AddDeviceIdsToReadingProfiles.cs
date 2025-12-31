using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceIdsToReadingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SeriesIds",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LibraryIds",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceIds",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceIds",
                table: "AppUserReadingProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "SeriesIds",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "LibraryIds",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "[]");
        }
    }
}
