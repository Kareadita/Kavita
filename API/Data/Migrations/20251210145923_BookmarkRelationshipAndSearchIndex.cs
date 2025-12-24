using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class BookmarkRelationshipAndSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean orphaned records BEFORE adding FK constraints
            migrationBuilder.Sql(@"
                DELETE FROM AppUserBookmark
                WHERE SeriesId NOT IN (SELECT Id FROM Series)
                   OR VolumeId NOT IN (SELECT Id FROM Volume)
                   OR ChapterId NOT IN (SELECT Id FROM Chapter);
            ");


            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_AppUserId",
                table: "AppUserBookmark");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadata_AgeRating",
                table: "SeriesMetadata",
                column: "AgeRating");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMetadata_SeriesId_AgeRating",
                table: "SeriesMetadata",
                columns: new[] { "SeriesId", "AgeRating" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_NormalizedName",
                table: "Series",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_MangaFile_FilePath",
                table: "MangaFile",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_Chapter_TitleName",
                table: "Chapter",
                column: "TitleName");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_AppUserId_SeriesId",
                table: "AppUserBookmark",
                columns: new[] { "AppUserId", "SeriesId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_ChapterId",
                table: "AppUserBookmark",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_SeriesId",
                table: "AppUserBookmark",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_VolumeId",
                table: "AppUserBookmark",
                column: "VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserBookmark_Chapter_ChapterId",
                table: "AppUserBookmark",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserBookmark_Series_SeriesId",
                table: "AppUserBookmark",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserBookmark_Volume_VolumeId",
                table: "AppUserBookmark",
                column: "VolumeId",
                principalTable: "Volume",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserBookmark_Chapter_ChapterId",
                table: "AppUserBookmark");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserBookmark_Series_SeriesId",
                table: "AppUserBookmark");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUserBookmark_Volume_VolumeId",
                table: "AppUserBookmark");

            migrationBuilder.DropIndex(
                name: "IX_SeriesMetadata_AgeRating",
                table: "SeriesMetadata");

            migrationBuilder.DropIndex(
                name: "IX_SeriesMetadata_SeriesId_AgeRating",
                table: "SeriesMetadata");

            migrationBuilder.DropIndex(
                name: "IX_Series_NormalizedName",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_MangaFile_FilePath",
                table: "MangaFile");

            migrationBuilder.DropIndex(
                name: "IX_Chapter_TitleName",
                table: "Chapter");

            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_AppUserId_SeriesId",
                table: "AppUserBookmark");

            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_ChapterId",
                table: "AppUserBookmark");

            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_SeriesId",
                table: "AppUserBookmark");

            migrationBuilder.DropIndex(
                name: "IX_AppUserBookmark_VolumeId",
                table: "AppUserBookmark");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserBookmark_AppUserId",
                table: "AppUserBookmark",
                column: "AppUserId");
        }
    }
}
