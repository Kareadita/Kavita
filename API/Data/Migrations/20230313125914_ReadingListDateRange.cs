using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadingListDateRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndingMonth",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EndingYear",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingMonth",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingYear",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "BookReaderWritingStyle",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndingMonth",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "EndingYear",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "StartingMonth",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "StartingYear",
                table: "ReadingList");

            migrationBuilder.AlterColumn<int>(
                name: "BookReaderWritingStyle",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 0);
        }
    }
}
