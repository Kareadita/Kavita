using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserBasedCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppUserCollectionId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppUserCollection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Promoted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CoverImage = table.Column<string>(type: "TEXT", nullable: true),
                    CoverImageLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    AgeRating = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserCollection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserCollection_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Series_AppUserCollectionId",
                table: "Series",
                column: "AppUserCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserCollection_AppUserId",
                table: "AppUserCollection",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Series_AppUserCollection_AppUserCollectionId",
                table: "Series",
                column: "AppUserCollectionId",
                principalTable: "AppUserCollection",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Series_AppUserCollection_AppUserCollectionId",
                table: "Series");

            migrationBuilder.DropTable(
                name: "AppUserCollection");

            migrationBuilder.DropIndex(
                name: "IX_Series_AppUserCollectionId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "AppUserCollectionId",
                table: "Series");
        }
    }
}
