using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationsHtmlContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentHtml",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentPlainText",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAnnotation_SeriesId",
                table: "AppUserAnnotation",
                column: "SeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserAnnotation_Series_SeriesId",
                table: "AppUserAnnotation",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUserAnnotation_Series_SeriesId",
                table: "AppUserAnnotation");

            migrationBuilder.DropIndex(
                name: "IX_AppUserAnnotation_SeriesId",
                table: "AppUserAnnotation");

            migrationBuilder.DropColumn(
                name: "CommentHtml",
                table: "AppUserAnnotation");

            migrationBuilder.DropColumn(
                name: "CommentPlainText",
                table: "AppUserAnnotation");
        }
    }
}
