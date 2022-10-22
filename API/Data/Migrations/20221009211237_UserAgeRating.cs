using API.Entities.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class UserAgeRating : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeRestriction",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: AgeRating.NotApplicable);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRestriction",
                table: "AspNetUsers");
        }
    }
}
