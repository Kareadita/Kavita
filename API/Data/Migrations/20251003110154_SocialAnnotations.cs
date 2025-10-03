using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SocialAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SocialPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"ShareReviews\":false,\"ShareAnnotations\":false,\"ViewOtherAnnotations\":false,\"SocialLibraries\":[],\"SocialMaxAgeRating\":-1,\"SocialIncludeUnknowns\":true}");

            migrationBuilder.AddColumn<string>(
                name: "Likes",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAnnotation_LibraryId",
                table: "AppUserAnnotation",
                column: "LibraryId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserAnnotation_Library_LibraryId",
                table: "AppUserAnnotation",
                column: "LibraryId",
                principalTable: "Library",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserAnnotation_Library_LibraryId",
                table: "AppUserAnnotation");

            migrationBuilder.DropIndex(
                name: "IX_AppUserAnnotation_LibraryId",
                table: "AppUserAnnotation");

            migrationBuilder.DropColumn(
                name: "SocialPreferences",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "AppUserAnnotation");
        }
    }
}
