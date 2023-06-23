using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScrobbleReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameLocked",
                table: "Series");

            migrationBuilder.AddColumn<string>(
                name: "ReviewBody",
                table: "ScrobbleEvent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewTitle",
                table: "ScrobbleEvent",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewBody",
                table: "ScrobbleEvent");

            migrationBuilder.DropColumn(
                name: "ReviewTitle",
                table: "ScrobbleEvent");

            migrationBuilder.AddColumn<bool>(
                name: "NameLocked",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
