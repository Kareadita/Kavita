using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ServerSettingsAdjustment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ServerSetting");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ServerSetting");

            migrationBuilder.AddColumn<string>(
                name: "CacheDirectory",
                table: "ServerSetting",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CacheDirectory",
                table: "ServerSetting");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ServerSetting",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "ServerSetting",
                type: "TEXT",
                maxLength: 65535,
                nullable: false,
                defaultValue: "");
        }
    }
}
