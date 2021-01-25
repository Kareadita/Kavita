using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ServerSettingsChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CacheDirectory",
                table: "ServerSetting",
                newName: "Value");

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "ServerSetting",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                table: "ServerSetting");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "ServerSetting",
                newName: "CacheDirectory");
        }
    }
}
