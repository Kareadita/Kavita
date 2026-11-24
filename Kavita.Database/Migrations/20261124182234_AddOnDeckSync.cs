using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOnDeckSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChapterPositions",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "OnDeckIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"CreateOnDeckCollection\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChapterPositions",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "OnDeckIds",
                table: "KoboSyncPoints");

            migrationBuilder.AlterColumn<string>(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"CreateOnDeckCollection\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}");
        }
    }
}
