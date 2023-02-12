using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class ReadingListFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlternateCount",
                table: "Chapter",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AlternateNumber",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlternateSeries",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoryArc",
                table: "Chapter",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoryArcNumber",
                table: "Chapter",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternateCount",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "AlternateNumber",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "AlternateSeries",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "StoryArc",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "StoryArcNumber",
                table: "Chapter");
        }
    }
}
