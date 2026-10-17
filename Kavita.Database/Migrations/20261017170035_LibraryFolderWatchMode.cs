using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class LibraryFolderWatchMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The bool is stored as 0 or 1, which are FolderWatchMode.Off and FolderWatchMode.Automatic
            migrationBuilder.RenameColumn(
                name: "FolderWatching",
                table: "Library",
                newName: "FolderWatchMode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FolderWatchMode",
                table: "Library",
                newName: "FolderWatching");

            migrationBuilder.Sql("UPDATE \"Library\" SET \"FolderWatching\" = 1 WHERE \"FolderWatching\" > 1;");
        }
    }
}
