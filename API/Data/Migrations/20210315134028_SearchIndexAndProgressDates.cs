using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class SearchIndexAndProgressDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "AppUserProgresses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "AppUserProgresses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId",
                table: "Series",
                columns: new[] { "Name", "NormalizedName", "LocalizedName", "LibraryId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Name_NormalizedName_LocalizedName_LibraryId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "AppUserProgresses");
        }
    }
}
