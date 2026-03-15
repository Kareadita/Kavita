using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReadingListSyncSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadUrl",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncCheckUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "ReadingList",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShaHash",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePath",
                table: "ReadingList",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadUrl",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "LastSyncCheckUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "ShaHash",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "SourcePath",
                table: "ReadingList");
        }
    }
}
