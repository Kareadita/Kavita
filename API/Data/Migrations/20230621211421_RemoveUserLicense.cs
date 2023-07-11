using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserLicense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "License",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "MalId",
                table: "ScrobbleEvent",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MalId",
                table: "ScrobbleEvent");

            migrationBuilder.AddColumn<string>(
                name: "License",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }
    }
}
