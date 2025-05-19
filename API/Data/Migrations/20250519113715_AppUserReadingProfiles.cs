using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppUserReadingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultReadingProfileId",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppUserReadingProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    PdfSpreadMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Implicit = table.Column<bool>(type: "INTEGER", nullable: false),
                    AppUserPreferencesId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserReadingProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserReadingProfile_AppUserPreferences_AppUserPreferencesId",
                        column: x => x.AppUserPreferencesId,
                        principalTable: "AppUserPreferences",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppUserReadingProfile_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryReadingProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadingProfileId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryReadingProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryReadingProfile_AppUserReadingProfile_ReadingProfileId",
                        column: x => x.ReadingProfileId,
                        principalTable: "AppUserReadingProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryReadingProfile_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryReadingProfile_Library_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Library",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesReadingProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadingProfileId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesReadingProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesReadingProfile_AppUserReadingProfile_ReadingProfileId",
                        column: x => x.ReadingProfileId,
                        principalTable: "AppUserReadingProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesReadingProfile_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesReadingProfile_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingProfile_AppUserPreferencesId",
                table: "AppUserReadingProfile",
                column: "AppUserPreferencesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingProfile_UserId",
                table: "AppUserReadingProfile",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryReadingProfile_AppUserId",
                table: "LibraryReadingProfile",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryReadingProfile_LibraryId_AppUserId",
                table: "LibraryReadingProfile",
                columns: new[] { "LibraryId", "AppUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryReadingProfile_ReadingProfileId",
                table: "LibraryReadingProfile",
                column: "ReadingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesReadingProfile_AppUserId",
                table: "SeriesReadingProfile",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesReadingProfile_ReadingProfileId",
                table: "SeriesReadingProfile",
                column: "ReadingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesReadingProfile_SeriesId",
                table: "SeriesReadingProfile",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryReadingProfile");

            migrationBuilder.DropTable(
                name: "SeriesReadingProfile");

            migrationBuilder.DropTable(
                name: "AppUserReadingProfile");

            migrationBuilder.DropColumn(
                name: "DefaultReadingProfileId",
                table: "AppUserPreferences");
        }
    }
}
