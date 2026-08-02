using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowKoboSyncLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowKoboSync",
                table: "Library",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowKoboSync",
                table: "Library");
        }
    }
}
