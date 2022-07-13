using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class RenamedBookReaderLayoutMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PageLayoutMode",
                table: "AppUserPreferences",
                newName: "BookReaderLayoutMode");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BookReaderLayoutMode",
                table: "AppUserPreferences",
                newName: "PageLayoutMode");
        }
    }
}
