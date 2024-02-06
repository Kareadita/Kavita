using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleEventError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "VolumeNumber",
                table: "ScrobbleEvent",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorDetails",
                table: "ScrobbleEvent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsErrored",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorDetails",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "IsErrored",
                table: "ScrobbleEvent");

            migrationBuilder.AlterColumn<int>(
                name: "VolumeNumber",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "REAL",
                oldNullable: true);
        }
    }
}
