using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class TimeEstimateInDB : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvgHoursToRead",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxHoursToRead",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinHoursToRead",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "WordCount",
                table: "Volume",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AvgHoursToRead",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxHoursToRead",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinHoursToRead",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvgHoursToRead",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxHoursToRead",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinHoursToRead",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgHoursToRead",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MaxHoursToRead",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "MinHoursToRead",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "WordCount",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "AvgHoursToRead",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MaxHoursToRead",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MinHoursToRead",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "AvgHoursToRead",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "MaxHoursToRead",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "MinHoursToRead",
                table: "Chapter");
        }
    }
}
