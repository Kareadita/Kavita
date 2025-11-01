using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpdsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OpdsPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"EmbedProgressIndicator\":true,\"IncludeContinueFrom\":true}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpdsPreferences",
                table: "AppUserPreferences");
        }
    }
}
