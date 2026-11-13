using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackKoboStoreSyncTokenForSyncPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KoboStoreSyncToken",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedChapterIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"AllLibraries\":false,\"SyncLibraries\":[],\"SyncCollection\":0,\"IsSyncingCollection\":false}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KoboStoreSyncToken",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "RemovedChapterIds",
                table: "KoboSyncPoints");

            migrationBuilder.AlterColumn<string>(
                name: "KoboSyncPreferences",
                table: "AppUserPreferences",
                type: "TEXT",
                nullable: true,
                defaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"AllLibraries\":false,\"SyncLibraries\":[],\"SyncCollection\":0,\"IsSyncingCollection\":false}",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "{\"SyncItemLimit\":100,\"ForwardToKoboStore\":true,\"LibrarySyncEnabled\":false,\"SyncLibraries\":[],\"ReadingListSyncEnabled\":false,\"SyncReadingLists\":[],\"CollectionSyncEnabled\":false,\"SyncCollections\":[],\"ManagedCollection\":0,\"HasManagedCollection\":false}");
        }
    }
}
