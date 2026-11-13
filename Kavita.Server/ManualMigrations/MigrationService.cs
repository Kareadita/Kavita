using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.EnvironmentInfo;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Kavita.Server.ManualMigrations.v0._7._11;
using Kavita.Server.ManualMigrations.v0._7._14;
using Kavita.Server.ManualMigrations.v0._7._9;
using Kavita.Server.ManualMigrations.v0._8._0;
using Kavita.Server.ManualMigrations.v0._8._1;
using Kavita.Server.ManualMigrations.v0._8._2;
using Kavita.Server.ManualMigrations.v0._8._4;
using Kavita.Server.ManualMigrations.v0._8._5;
using Kavita.Server.ManualMigrations.v0._8._6;
using Kavita.Server.ManualMigrations.v0._8._7;
using Kavita.Server.ManualMigrations.v0._8._8;
using Kavita.Server.ManualMigrations.v0._8._9;
using Kavita.Server.ManualMigrations.v0._9._0;
using Kavita.Server.ManualMigrations.v0._9._1;
using Kavita.Server.ManualMigrations.v0._9._1.x;
using Kavita.Server.ManualMigrations.v0._9._2;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations;

public interface IMigrationService
{
    /// <summary>
    /// Takes a snapshot of kavita.db when this start will migrate it. Throws when the snapshot cannot be written
    /// </summary>
    Task CreateSnapshot();
    /// <summary>
    /// Runs manual migrations that MUST run before EF migrations
    /// </summary>
    Task RunPreMigrations();
    /// <summary>
    /// Runs manual migrations that run after EF migrations and seeding
    /// </summary>
    Task RunPostMigrations();
    /// <summary>
    /// Deletes this run's snapshot once every migration has succeeded
    /// </summary>
    void DeleteSnapshot();
    /// <summary>
    /// Copies this run's snapshot back over kavita.db. Does nothing when this run took no snapshot
    /// </summary>
    Task RestoreDatabase(Exception ex);
}

public class MigrationService(IUnitOfWork unitOfWork, DataContext context, IVersionUpdaterService versionService, IDirectoryService directoryService,
    IBackupService backupService, ILogger<MigrationService> logger) : IMigrationService
{
    private const string DatabaseFile = "kavita.db";
    private const bool SnapshotOnVersionChange = false;

    private string? _snapshotFile;

    public async Task CreateSnapshot()
    {
        if (!await context.Database.CanConnectAsync()) return;

        var installVersion = await ReadInstallVersion();
        var efPending = (await context.Database.GetPendingMigrationsAsync()).Any();
        var versionChanged = SnapshotOnVersionChange && installVersion != BuildInfo.Version.ToString();
        if (!efPending && !versionChanged) return;

        var fs = directoryService.FileSystem;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var folderName = string.IsNullOrEmpty(installVersion) ? $"unknown_{stamp}" : $"v{installVersion}_{stamp}";
        var snapshotDirectory = fs.Path.Join(directoryService.MigrationDirectory, folderName);
        var snapshotFile = fs.Path.Join(snapshotDirectory, DatabaseFile);

        logger.LogInformation("Migrations are needed, taking a snapshot of the database at {SnapshotFile}", snapshotFile);
        directoryService.ExistOrCreate(snapshotDirectory);
        await backupService.SnapshotDatabase(snapshotFile);

        _snapshotFile = snapshotFile;
    }

    public async Task RunPreMigrations()
    {
        if (!await context.Database.CanConnectAsync()) return;

        logger.LogInformation("Running Manual Migrations");

        try
        {
            // v0.7.14
            await new MigrateWantToReadExport(directoryService).RunAsync(context, logger);

            // v0.8.2
            await ManualMigrateSwitchToWal.Migrate(context, logger);

            // v0.8.4
            await ManualMigrateEncodeSettings.Migrate(context, logger);

            // v0.9.2, before StartupTasksHostedService reads the setting
            await new ManualMigrateFolderWatchingOn().RunAsync(context, logger);
        }
        catch (Exception ex) when (_snapshotFile == null)
        {
            logger.LogCritical(ex, "A manual migration failed and there is no snapshot to restore, continuing startup");
        }

        await unitOfWork.CommitAsync();
        logger.LogInformation("Running Manual Migrations - complete");
    }

    public async Task RunPostMigrations()
    {
        try
        {
            logger.LogInformation("Running Migrations");

            #region Migrations

            #region v0.7.9
            await MigrateUserLibrarySideNavStream.Migrate(unitOfWork, context, logger);
            #endregion

            #region v0.7.11
            await MigrateSmartFilterEncoding.Migrate(unitOfWork, context, logger);
            await new MigrateLibrariesToHaveAllFileTypes().RunAsync(context, logger);
            #endregion

            #region v0.7.14
            await MigrateEmailTemplates.Migrate(directoryService, logger);
            await MigrateVolumeNumber.Migrate(context, logger);
            await new MigrateWantToReadImport(unitOfWork, directoryService).RunAsync(context, logger);
            await MigrateManualHistory.Migrate(context, logger);
            await MigrateClearNightlyExternalSeriesRecords.Migrate(context, logger);
            #endregion

            #region v0.8.0
            await MigrateVolumeLookupName.Migrate(context, unitOfWork, logger);
            await MigrateChapterNumber.Migrate(context, logger);
            await MigrateProgressExport.Migrate(context, directoryService, logger);
            await MigrateMixedSpecials.Migrate(context, unitOfWork, directoryService, logger);
            await MigrateLooseLeafChapters.Migrate(context, unitOfWork, directoryService, logger);
            await MigrateChapterFields.Migrate(context, unitOfWork, logger);
            await MigrateChapterRange.Migrate(context, unitOfWork, logger);
            await MigrateMangaFilePath.Migrate(context, logger);
            await MigrateCollectionTagToUserCollections.Migrate(context, unitOfWork, logger);
            #endregion

            #region v0.8.1
            await MigrateLowestSeriesFolderPath.Migrate(context, unitOfWork, logger);
            #endregion

            #region v0.8.2
            await ManualMigrateThemeDescription.Migrate(context, logger);
            await MigrateInitialInstallData.Migrate(context, logger, directoryService);
            await MigrateSeriesLowestFolderPath.Migrate(context, logger, directoryService);
            #endregion

            #region v0.8.4
            await MigrateLowestSeriesFolderPath2.Migrate(context, unitOfWork, logger);
            await ManualMigrateRemovePeople.Migrate(context, logger);
            await MigrateDuplicateDarkTheme.Migrate(context, logger);
            await ManualMigrateUnscrobbleBookLibraries.Migrate(context, logger);
            #endregion

            #region v0.8.5
            await ManualMigrateBlacklistTableToSeries.Migrate(context, logger);
            await ManualMigrateInvalidBlacklistSeries.Migrate(context, logger);
            await ManualMigrateScrobbleErrors.Migrate(context, logger);
            await ManualMigrateNeedsManualMatch.Migrate(context, logger);
            await MigrateProgressExportForV085.Migrate(context, directoryService, logger);
            #endregion

            #region v0.8.6
            await ManualMigrateScrobbleSpecials.Migrate(context, logger);
            await ManualMigrateScrobbleEventGen.Migrate(context, logger);
            #endregion

            #region v0.8.7
            await ManualMigrateReadingProfiles.Migrate(context, logger);
            #endregion

            #region v0.8.8
            await ManualMigrateEnableMetadataMatchingDefault.Migrate(context, unitOfWork, logger);
            await ManualMigrateBookReadingProgress.Migrate(context, unitOfWork, logger);
            #endregion

            #region v0.8.9
            await new MigrateBadKoreaderProgress().RunAsync(context, logger);
            await new MigrateProgressToReadingSessions().RunAsync(context, logger);
            await new MigrateMissingCreatedUtcDate().RunAsync(context, logger);
            await new MigrateTotalReads().RunAsync(context, logger);
            await new MigrateToAuthKeys().RunAsync(context, logger);
            await new MigrateMissingAppUserRatingDateColumns().RunAsync(context, logger);
            await new MigrateFormatToActivityDataV2().RunAsync(context, logger);
            await new MigrateIncorrectUtcTimes().RunAsync(context, logger);
            #endregion

            #region v0.9.0
            await new ManualMigrateEnsureNoReadOnlyAdmins().RunAsync(context, logger);
            await new ManualMigrationRemoveMoreInGenreStream().RunAsync(context, logger);
            await new ManualMigrateSmartFilterEntityTypeBackfill().RunAsync(context, logger);
            await new ManualMigrateEpubFontFamilyDetailsBackfill().RunAsync(context, logger);
            #endregion

            #region v0.9.1

            await new ManualMigrationScrobbleRework().RunAsync(context, logger);
            await new ManualMigrationKavitaScrobbleProviders().RunAsync(context, logger);
            await new ManualMigrationMetadataProvider().RunAsync(context, logger);
            await new ManualMigrationOAuthMigration().RunAsync(context, logger);
            await new ManualMigrateRelationshipAuditHistory().RunAsync(context, logger);
            await new ManualMigrationSetDefaultMetadataProvidersForLibrary().RunAsync(context, logger);
            await new ManualMigrationMetadataSettingFieldRenumber().RunAsync(context, logger);
            await new ManualMigrateOriginalNameBackfill().RunAsync(context, logger);
            await new ManualMigrateNormalizedOriginalNameBackfill().RunAsync(context, logger);
            await new ManualMigrateLastReadDates().RunAsync(context, logger);
            await new ManualMigrateVersionCacheFiles(directoryService).RunAsync(context, logger);
            await new ManualMigrationExternalSeriesMetadataIds().RunAsync(context, logger);
            await new ManualMigrationCorrectAuditStatus().RunAsync(context, logger);
            await new ManualMigrateCoverImageSettings().RunAsync(context, logger);

            await new ManualMigrateBreakpointMapping().RunAsync(context, logger);

            #endregion

            #region v0.9.2

            await new ManualMigrationUnassignDefaultReadingProfile().RunAsync(context, logger);
            await new ManualMigrateOnDeckSettings().RunAsync(context, logger);
            await new ManualMigrationFixInvalidOnDeckSettings().RunAsync(context, logger);
            await new ManualMigrationClearRootLowestFolderPath().RunAsync(context, logger);
            await new ManualMigrateRemoveLegacyGithubCaches(directoryService).RunAsync(context, logger);
            await new ManualMigrateImageFileBackfill(directoryService).RunAsync(context, logger);

            #endregion

            #endregion

            //  Update the version in the DB after all migrations are run
            var installVersion = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion);
            var isVersionDifferent = installVersion.Value != BuildInfo.Version.ToString();
            installVersion.Value = BuildInfo.Version.ToString();
            unitOfWork.SettingsRepository.Update(installVersion);
            await unitOfWork.CommitAsync();

            logger.LogInformation("Running Migrations - complete");

            if (isVersionDifferent)
            {
                // Clear the GitHub cache so update stuff shows correctly
                versionService.BustGithubCache();
            }
        }
        catch (Exception ex) when (_snapshotFile == null)
        {
            logger.LogCritical(ex, "A migration failed and there is no snapshot to restore, continuing startup");
        }
    }

    public void DeleteSnapshot()
    {
        if (_snapshotFile == null) return;

        var snapshotDirectory = directoryService.FileSystem.Path.GetDirectoryName(_snapshotFile)!;
        directoryService.ClearAndDeleteDirectory(snapshotDirectory);
        logger.LogInformation("Removed the database snapshot {SnapshotDirectory}", snapshotDirectory);

        _snapshotFile = null;
    }

    public async Task RestoreDatabase(Exception ex)
    {
        if (_snapshotFile == null)
        {
            logger.LogCritical(ex, "Kavita failed during startup. No snapshot was taken this run, leaving the database as is");
            return;
        }

        var fs = directoryService.FileSystem;
        var databaseFile = fs.Path.Join(directoryService.ConfigDirectory, DatabaseFile);
        var walFile = databaseFile + "-wal";
        logger.LogCritical(ex, "A migration failed during startup. Restoring the database from {SnapshotFile} and exiting", _snapshotFile);

        // Pooled connections keep kavita.db open, and Windows refuses to delete or overwrite an open file. Pools only release closed connections
        await context.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        // The snapshot is a VACUUM INTO copy with no WAL, so the failed run's -wal must not be replayed onto it
        directoryService.DeleteFiles([databaseFile + "-shm", walFile]);
        directoryService.CopyFileToDirectory(_snapshotFile, directoryService.ConfigDirectory);

        var restored = !fs.File.Exists(walFile)
                       && fs.File.Exists(databaseFile)
                       && fs.FileInfo.New(databaseFile).Length == fs.FileInfo.New(_snapshotFile).Length;
        if (!restored)
        {
            logger.LogCritical("The database could not be restored. With Kavita stopped, delete {WalFile} and copy {SnapshotFile} over {DatabaseFile}",
                walFile, _snapshotFile, databaseFile);
            return;
        }

        logger.LogInformation("Database restored from {SnapshotFile}", _snapshotFile);
        DeleteSnapshot();
    }

    private async Task<string?> ReadInstallVersion()
    {
        try
        {
            return await context.ServerSetting
                .Where(s => s.Key == ServerSettingKey.InstallVersion)
                .Select(s => s.Value)
                .SingleOrDefaultAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the install version, naming the snapshot unknown");
            return null;
        }
    }
}
