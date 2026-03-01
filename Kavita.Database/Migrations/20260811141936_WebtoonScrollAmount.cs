using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class WebtoonScrollAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WebtoonScrollAmount",
                table: "AppUserReadingProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 85);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebtoonScrollAmount",
                table: "AppUserReadingProfiles");
        }
    }
}
