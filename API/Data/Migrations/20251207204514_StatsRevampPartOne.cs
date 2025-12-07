using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class StatsRevampPartOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "AppUserRating");

            migrationBuilder.AddColumn<string>(
                name: "CoverImage",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "AppUserRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AppUserRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "AppUserRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "AppUserRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalReads",
                table: "AppUserProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "SocialPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"ShareReviews\":false,\"ShareAnnotations\":false,\"ViewOtherAnnotations\":false,\"SocialLibraries\":[],\"SocialMaxAgeRating\":-1,\"SocialIncludeUnknowns\":true,\"ShareProfile\":false}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"ShareReviews\":false,\"ShareAnnotations\":false,\"ViewOtherAnnotations\":false,\"SocialLibraries\":[],\"SocialMaxAgeRating\":-1,\"SocialIncludeUnknowns\":true}");

            migrationBuilder.AddColumn<int>(
                name: "PromptForRereadsAfter",
                table: "AppUserPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "AppUserChapterRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "AppUserChapterRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "AppUserChapterRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "AppUserChapterRating",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "AppUserAuthKey",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserAuthKey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserAuthKey_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserReadingHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "{\"TotalMinutesRead\":0,\"TotalPagesRead\":0,\"TotalWordsRead\":0,\"LongestSessionMinutes\":0,\"SeriesIds\":null,\"ChapterIds\":null}"),
                    ClientInfoUsed = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "[]"),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserReadingHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserReadingHistory_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserReadingSession",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserReadingSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserReadingSession_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientDevice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UiFingerprint = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentClientInfo = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{\"UserAgent\":\"\",\"IpAddress\":\"\",\"AuthType\":0,\"ClientType\":0,\"AppVersion\":null,\"Browser\":null,\"BrowserVersion\":null,\"Platform\":0,\"DeviceType\":null,\"ScreenWidth\":null,\"ScreenHeight\":null,\"Orientation\":null,\"CapturedAt\":\"0001-01-01T00:00:00\"}"),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDevice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDevice_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserReadingSessionActivityData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserReadingSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartPage = table.Column<int>(type: "INTEGER", nullable: false),
                    EndPage = table.Column<int>(type: "INTEGER", nullable: false),
                    StartBookScrollId = table.Column<string>(type: "TEXT", nullable: true),
                    EndBookScrollId = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PagesRead = table.Column<int>(type: "INTEGER", nullable: false),
                    WordsRead = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPages = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWords = table.Column<long>(type: "INTEGER", nullable: false),
                    DeviceIds = table.Column<string>(type: "TEXT", nullable: false),
                    ClientInfo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserReadingSessionActivityData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUserReadingSessionActivityData_AppUserReadingSession_AppUserReadingSessionId",
                        column: x => x.AppUserReadingSessionId,
                        principalTable: "AppUserReadingSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserReadingSessionActivityData_Chapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserReadingSessionActivityData_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientDeviceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfo = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "{\"UserAgent\":\"\",\"IpAddress\":\"\",\"AuthType\":0,\"ClientType\":0,\"AppVersion\":null,\"Browser\":null,\"BrowserVersion\":null,\"Platform\":0,\"DeviceType\":null,\"ScreenWidth\":null,\"ScreenHeight\":null,\"Orientation\":null,\"CapturedAt\":\"0001-01-01T00:00:00\"}"),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDeviceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDeviceHistory_ClientDevice_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "ClientDevice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAuthKey_AppUserId",
                table: "AppUserAuthKey",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAuthKey_ExpiresAtUtc",
                table: "AppUserAuthKey",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserAuthKey_Key",
                table: "AppUserAuthKey",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingHistory_AppUserId",
                table: "AppUserReadingHistory",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingHistory_DateUtc",
                table: "AppUserReadingHistory",
                column: "DateUtc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSession_AppUserId",
                table: "AppUserReadingSession",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSession_IsActive",
                table: "AppUserReadingSession",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSessionActivityData_AppUserReadingSessionId",
                table: "AppUserReadingSessionActivityData",
                column: "AppUserReadingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSessionActivityData_ChapterId",
                table: "AppUserReadingSessionActivityData",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserReadingSessionActivityData_SeriesId",
                table: "AppUserReadingSessionActivityData",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDevice_AppUserId",
                table: "ClientDevice",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDeviceHistory_DeviceId",
                table: "ClientDeviceHistory",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserAuthKey");

            migrationBuilder.DropTable(
                name: "AppUserReadingHistory");

            migrationBuilder.DropTable(
                name: "AppUserReadingSessionActivityData");

            migrationBuilder.DropTable(
                name: "ClientDeviceHistory");

            migrationBuilder.DropTable(
                name: "AppUserReadingSession");

            migrationBuilder.DropTable(
                name: "ClientDevice");

            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "AppUserRating");

            migrationBuilder.DropColumn(
                name: "TotalReads",
                table: "AppUserProgresses");

            migrationBuilder.DropColumn(
                name: "PromptForRereadsAfter",
                table: "AppUserPreferences");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "AppUserChapterRating");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "AppUserChapterRating");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "AppUserChapterRating");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "AppUserChapterRating");

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "AppUserRating",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SocialPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"ShareReviews\":false,\"ShareAnnotations\":false,\"ViewOtherAnnotations\":false,\"SocialLibraries\":[],\"SocialMaxAgeRating\":-1,\"SocialIncludeUnknowns\":true}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"ShareReviews\":false,\"ShareAnnotations\":false,\"ViewOtherAnnotations\":false,\"SocialLibraries\":[],\"SocialMaxAgeRating\":-1,\"SocialIncludeUnknowns\":true,\"ShareProfile\":false}");
        }
    }
}
