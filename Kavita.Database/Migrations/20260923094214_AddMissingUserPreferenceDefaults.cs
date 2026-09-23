using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingUserPreferenceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OnDeckUpdateDays",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "OnDeckProgressDays",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OnDeckUpdateDays",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 7);

            migrationBuilder.AlterColumn<int>(
                name: "OnDeckProgressDays",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 30);
        }
    }
}
