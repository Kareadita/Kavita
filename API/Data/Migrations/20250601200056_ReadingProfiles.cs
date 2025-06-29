using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserReadingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryIds = table.Column<string>(type: "TEXT", nullable: true),
                    SeriesIds = table.Column<string>(type: "TEXT", nullable: true),
                    ReadingDirection = table.Column<int>(type: "INTEGER", nullable: false),
                    ScalingOption = table.Column<int>(type: "INTEGER", nullable: false),
                    PageSplitOption = table.Column<int>(type: "INTEGER", nullable: false),
                    ReaderMode = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoCloseMenu = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowScreenHints = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmulateBook = table.Column<bool>(type: "INTEGER", nullable: false),
                    LayoutMode = table.Column<int>(type: "INTEGER", nullable: false),
                    BackgroundColor = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "#000000"),
                    SwipeToPaginate = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowAutomaticWebtoonReaderDetection = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    WidthOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    BookReaderMargin = table.Column<int>(type: "INTEGER", nullable: false),
                    BookReaderLineSpacing = table.Column<int>(type: "INTEGER", nullable: false),
                    BookReaderFontSize = table.Column<int>(type: "INTEGER", nullable: false),
                    BookReaderFontFamily = table.Column<string>(type: "TEXT", nullable: true),
                    BookReaderTapToPaginate = table.Column<bool>(type: "INTEGER", nullable: false),
                    BookReaderReadingDirection = table.Column<int>(type: "INTEGER", nullable: false),
                    BookReaderWritingStyle = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BookThemeName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "Dark"),
                    BookReaderLayoutMode = table.Column<int>(type: "INTEGER", nullable: false),
                    BookReaderImmersiveMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    PdfTheme = table.Column<int>(type: "INTEGER", nullable: false),
                    PdfScrollMode = table.Column<int>(type: "INTEGER", nullable: false),
                    PdfSpreadMode = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserReadingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserReadingProfiles_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingProfiles_AppUserId",
                table: "AppUserReadingProfiles",
                column: "AppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserReadingProfiles");
        }
    }
}
