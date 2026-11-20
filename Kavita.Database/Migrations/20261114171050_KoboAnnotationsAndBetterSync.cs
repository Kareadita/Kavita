using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class KoboAnnotationsAndBetterSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectionsSyncTime",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CollectionsSyncedUntilIndex",
                table: "KoboSyncPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KoboStoreSyncToken",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReadingListsIds",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadingListsSyncTime",
                table: "KoboSyncPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ReadingListsSyncedUntilIndex",
                table: "KoboSyncPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<string>(
                name: "KoboId",
                table: "AppUserAnnotation",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionsIds",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "CollectionsSyncTime",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "CollectionsSyncedUntilIndex",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "KoboStoreSyncToken",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "ReadingListsIds",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "ReadingListsSyncTime",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "ReadingListsSyncedUntilIndex",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "RemovedChapterIds",
                table: "KoboSyncPoints");

            migrationBuilder.DropColumn(
                name: "KoboId",
                table: "AppUserAnnotation");

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
