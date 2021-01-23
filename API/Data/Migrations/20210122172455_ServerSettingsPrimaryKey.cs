using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class ServerSettingsPrimaryKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServerSetting",
                table: "ServerSetting");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ServerSetting");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ServerSetting",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServerSetting",
                table: "ServerSetting",
                column: "Key");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServerSetting",
                table: "ServerSetting");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ServerSetting",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ServerSetting",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServerSetting",
                table: "ServerSetting",
                column: "Id");
        }
    }
}
