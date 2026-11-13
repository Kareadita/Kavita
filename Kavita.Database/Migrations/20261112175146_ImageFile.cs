using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImageFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "Volume",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "ReadingList",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "Person",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "Library",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "Chapter",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverImageFileId",
                table: "AppUserCollection",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageFileId",
                table: "AppUserBookmark",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryColor = table.Column<string>(type: "TEXT", nullable: true),
                    SecondaryColor = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageFile", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Volume_CoverImageFileId",
                table: "Volume",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Series_CoverImageFileId",
                table: "Series",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingList_CoverImageFileId",
                table: "ReadingList",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_CoverImageFileId",
                table: "Person",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Library_CoverImageFileId",
                table: "Library",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapter_CoverImageFileId",
                table: "Chapter",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CoverImageFileId",
                table: "AspNetUsers",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserCollection_CoverImageFileId",
                table: "AppUserCollection",
                column: "CoverImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_ImageFileId",
                table: "AppUserBookmark",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageFile_FileName_Kind",
                table: "ImageFile",
                columns: new[] { "FileName", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageFile_Kind_Key",
                table: "ImageFile",
                columns: new[] { "Kind", "Key" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserBookmark_ImageFile_ImageFileId",
                table: "AppUserBookmark",
                column: "ImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserCollection_ImageFile_CoverImageFileId",
                table: "AppUserCollection",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ImageFile_CoverImageFileId",
                table: "AspNetUsers",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Chapter_ImageFile_CoverImageFileId",
                table: "Chapter",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Library_ImageFile_CoverImageFileId",
                table: "Library",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Person_ImageFile_CoverImageFileId",
                table: "Person",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingList_ImageFile_CoverImageFileId",
                table: "ReadingList",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Series_ImageFile_CoverImageFileId",
                table: "Series",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Volume_ImageFile_CoverImageFileId",
                table: "Volume",
                column: "CoverImageFileId",
                principalTable: "ImageFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserBookmark_ImageFile_ImageFileId",
                table: "AppUserBookmark");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserCollection_ImageFile_CoverImageFileId",
                table: "AppUserCollection");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ImageFile_CoverImageFileId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Chapter_ImageFile_CoverImageFileId",
                table: "Chapter");

            migrationBuilder.DropForeignKey(
                name: "FK_Library_ImageFile_CoverImageFileId",
                table: "Library");

            migrationBuilder.DropForeignKey(
                name: "FK_Person_ImageFile_CoverImageFileId",
                table: "Person");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingList_ImageFile_CoverImageFileId",
                table: "ReadingList");

            migrationBuilder.DropForeignKey(
                name: "FK_Series_ImageFile_CoverImageFileId",
                table: "Series");

            migrationBuilder.DropForeignKey(
                name: "FK_Volume_ImageFile_CoverImageFileId",
                table: "Volume");

            migrationBuilder.DropTable(
                name: "ImageFile");

            migrationBuilder.DropIndex(
                name: "IX_Volume_CoverImageFileId",
                table: "Volume");

            migrationBuilder.DropIndex(
                name: "IX_Series_CoverImageFileId",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_ReadingList_CoverImageFileId",
                table: "ReadingList");

            migrationBuilder.DropIndex(
                name: "IX_Person_CoverImageFileId",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Library_CoverImageFileId",
                table: "Library");

            migrationBuilder.DropIndex(
                name: "IX_Chapter_CoverImageFileId",
                table: "Chapter");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CoverImageFileId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUserCollection_CoverImageFileId",
                table: "AppUserCollection");

            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_ImageFileId",
                table: "AppUserBookmark");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "Volume");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "ReadingList");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "Library");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "Chapter");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CoverImageFileId",
                table: "AppUserCollection");

            migrationBuilder.DropColumn(
                name: "ImageFileId",
                table: "AppUserBookmark");
        }
    }
}
