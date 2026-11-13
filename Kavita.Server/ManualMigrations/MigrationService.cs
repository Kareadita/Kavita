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
    /// Returns the directory where the temp migration snapshot should reside
    /// </summary>
    /// <returns></returns>
    Task<string> GetMigrationDirectory();
    /// <summary>
    /// Is the DB Created and there are pending pre-migrations to run?
    /// </summary>
    /// <returns></returns>
    Task<bool> HasPendingMigrations();
    /// <summary>
    /// Runs migrations that should run before EF Migrations trigger
    /// </summary>
    /// <returns></returns>
    Task RunPreMigrations();
    /// <summary>
    /// Runs Manual Migrations that should run after EF Migrations trigger
    /// </summary>
    /// <returns></returns>
    Task RunPostMigrations();
    /// <summary>
    /// Whenever the migration (Pre/Post) fails, Kavita restores the db and related files
    /// </summary>
    /// <returns></returns>
    Task RestoreDatabase(Exception ex);
}


public class MigrationService(IUnitOfWork unitOfWork, DataContext context, IVersionUpdaterService versionService, IDirectoryService directoryService,
    IBackupService backupService, ILogger<MigrationService> logger) : IMigrationService
{
    public async Task<string> GetMigrationDirectory()
    {
        string? currentVersion = null;
        try
        {
            if (!await context.ServerSetting.AnyAsync()) return "vUnknown";
            currentVersion =
                (await context.ServerSetting.SingleOrDefaultAsync(s =>
                    s.Key == ServerSettingKey.InstallVersion))?.Value;
        }
        catch (Exception)
        {
            // ignored
        }

        if (string.IsNullOrEmpty(currentVersion))
        {
            currentVersion = "vUnknown";
        }

        return directoryService.FileSystem.Path.Join(directoryService.MigrationDirectory, currentVersion);
    }

    public async Task<bool> HasPendingMigrations()
    {
        var isDbCreated = await context.Database.CanConnectAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        return isDbCreated && pendingMigrations.Any();
    }

    public async Task RunPreMigrations()
    {
        var isDbCreated = await context.Database.CanConnectAsync();
        if (!isDbCreated) return;

        await CreateMigrationBackupSnapshot();

        logger.LogInformation("Running Manual Migrations");

        try
        {
            // v0.7.14
            await new MigrateWantToReadExport(directoryService).RunAsync(context, logger);

            // v0.8.2
            await ManualMigrateSwitchToWal.Migrate(context, logger);

            // v0.8.4
            await ManualMigrateEncodeSettings.Migrate(context, logger);

            // v0.9.2
            await new ManualMigrateFolderWatchingOn().RunAsync(context, logger);
        }
        catch (Exception)
        {
            /* Swallow */
        }

        await unitOfWork.CommitAsync();
        logger.LogInformation("Running Manual Migrations - complete");
    }

    public async Task RunPostMigrations()
    {
        try
        {
             await CreateMigrationBackupSnapshot();
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
        } catch (Exception ex)
        {
            logger.LogCritical(ex, "An error occurred during migration");
        }
    }

    public async Task RestoreDatabase(Exception ex)
    {
        var migrationDirectory = await GetMigrationDirectory();
        var fs = directoryService.FileSystem;
        var snapshotFile = fs.Path.Join(migrationDirectory, "kavita.db");

        if (!fs.File.Exists(snapshotFile))
        {
            logger.LogCritical(ex, "Kavita failed during startup and there is no migration snapshot at {SnapshotFile} to restore, leaving the database as is", snapshotFile);
            return;
        }

        logger.LogCritical(ex, "A migration failed during startup. Restoring backup from {MigrationDirectory} and exiting", migrationDirectory);

        // Pooled connections keep kavita.db open, and Windows refuses to delete or overwrite an open file
        SqliteConnection.ClearAllPools();

        // The snapshot is a VACUUM INTO copy with no WAL, so the failed run's -wal must not be replayed onto it
        directoryService.DeleteFiles([
            fs.Path.Join(directoryService.ConfigDirectory, "kavita.db-shm"),
            fs.Path.Join(directoryService.ConfigDirectory, "kavita.db-wal")
        ]);

        directoryService.CopyFileToDirectory(snapshotFile, directoryService.ConfigDirectory);
    }

    private async Task CreateMigrationBackupSnapshot()
    {
        if (!await HasPendingMigrations()) return;
        var migrationDirectory = await GetMigrationDirectory();

        logger.LogInformation("Performing backup as migrations are needed. Backup will be config/temp/kavita.db");
        directoryService.ExistOrCreate(migrationDirectory);

        var fs = directoryService.FileSystem;
        var snapshotFile = fs.Path.Join(migrationDirectory, "kavita.db");
        if (!fs.File.Exists(snapshotFile))
        {
            await backupService.SnapshotDatabase(snapshotFile);
            logger.LogInformation("Database backed up to {MigrationDirectory}", migrationDirectory);
        }
    }
}
