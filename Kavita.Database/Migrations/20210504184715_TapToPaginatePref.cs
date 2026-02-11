using Microsoft.EntityFrameworkCore.Migrations;

namespace Kavita.Database.Migrations
{
    public partial class TapToPaginatePref : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BookReaderTapToPaginate",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookReaderTapToPaginate",
                table: "AppUserPreferences");
        }
    }
}
