using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Data.Migrations
{
    public partial class EntityImageRefactor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AppUserProgresses");

            migrationBuilder.AlterColumn<string>(
                name: "CoverImage",
                table: "Volume",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImage",
                table: "Series",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImage",
                table: "CollectionTag",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImage",
                table: "Chapter",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "CoverImage",
                table: "Volume",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "CoverImage",
                table: "Series",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "CoverImage",
                table: "CollectionTag",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "CoverImage",
                table: "Chapter",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "RowVersion",
                table: "AppUserProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0u);
        }
    }
}
