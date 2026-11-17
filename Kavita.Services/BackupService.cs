using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.EnvironmentInfo;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class BackupService(
    ILogger<BackupService> logger,
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IImageFileService imageFileService,
    IEventHub eventHub)
    : IBackupService
{
    private readonly IList<string> _backupFiles =
    [
        "appsettings.json"
    ];

    /// <summary>
    /// Backups are prepared here and this is cleared before any backup runs. This avoids any clearing temp/ while building a backup
    /// </summary>
    private const string StagingDirectory = ".staging";
    private static readonly SemaphoreSlim BackupLock = new(1, 1);

    /// <summary>
    /// Returns a list of all log files for Kavita
    /// </summary>
    /// <param name="rollFiles">If file rolling is enabled. Defaults to True.</param>
    /// <returns></returns>
    public IEnumerable<string> GetLogFiles(bool rollFiles = true)
    {
        var multipleFileRegex = rollFiles ? @"\d*" : string.Empty;
        var fi = directoryService.FileSystem.FileInfo.New(IBackupService.LogFile);

        var files = rollFiles
            ? directoryService.GetFiles(directoryService.LogDirectory,
                $@"{directoryService.FileSystem.Path.GetFileNameWithoutExtension(fi.Name)}{multipleFileRegex}\.log")
            : [directoryService.FileSystem.Path.Join(directoryService.LogDirectory, "kavita.log")];
        return files;
    }

    /// <summary>
    /// Will back up anything that needs to be backed up. This includes logs, setting files, and images a scan/Kavita+
    /// refresh cannot rebuild: uploads, OIDC avatars, reading list and collection covers, favicons, publishers, etc.
    /// Covers from a scan or from Kavita+ are left out
    /// </summary>
    /// <returns>False when no zip was written</returns>
    public async Task<bool> BackupDatabase(CancellationToken ct = default)
    {
        await BackupLock.WaitAsync(ct);
        string? stagingDirectory = null;
        try
        {
            logger.LogInformation("Beginning backup of Database at {BackupTime}", DateTime.Now);
            var backupDirectory = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory, ct)).Value;

            logger.LogDebug("Backing up to {BackupDirectory}", backupDirectory);
            if (!directoryService.ExistOrCreate(backupDirectory))
            {
                logger.LogCritical("Could not write to {BackupDirectory}; aborting backup", backupDirectory);
                await eventHub.SendMessageAsync(MessageFactory.Error,
                    MessageFactory.BackupFolderUnwritableEvent(backupDirectory), ct: ct);
                return false;
            }

            stagingDirectory = Path.Join(backupDirectory, StagingDirectory);
            if (!directoryService.ExistOrCreate(stagingDirectory))
            {
                logger.LogCritical("Could not write to {BackupDirectory}; aborting backup", stagingDirectory);
                await eventHub.SendMessageAsync(MessageFactory.Error,
                    MessageFactory.BackupFolderUnwritableEvent(stagingDirectory), ct: ct);
                return false;
            }

            directoryService.ClearDirectory(stagingDirectory);

            await SendProgress(0F, "Started backup", ct);
            await SendProgress(0.1F, "Copying core files", ct);

            var dateString = $"{DateTime.UtcNow.ToShortDateString()}_{DateTime.UtcNow:s}Z".Replace("/", "_").Replace(":", "_");
            var zipPath = directoryService.FileSystem.Path.Join(backupDirectory, $"kavita_backup_v{BuildInfo.Version}_{dateString}.zip");

            if (File.Exists(zipPath))
            {
                logger.LogCritical("{ZipFile} already exists, aborting", zipPath);
                await eventHub.SendMessageAsync(MessageFactory.Error,
                    MessageFactory.BackupExistsEvent(zipPath), ct: ct);
                return false;
            }

            await SendProgress(0.1F, "Backing up database", ct);
            await SnapshotDatabase(directoryService.FileSystem.Path.Join(stagingDirectory, "kavita.db"), ct);

            await SendProgress(0.15F, "Copying config files", ct);
            directoryService.CopyFilesToDirectory(
                _backupFiles.Select(file => directoryService.FileSystem.Path.Join(directoryService.ConfigDirectory, file)), stagingDirectory);

            // Copy any csv's as those are used for manual migrations
            directoryService.CopyFilesToDirectory(
                directoryService.GetFilesWithCertainExtensions(directoryService.ConfigDirectory, @"\.csv"), stagingDirectory);

            await SendProgress(0.2F, "Copying logs", ct);
            CopyLogsToBackupDirectory(stagingDirectory);

            await SendProgress(0.25F, "Copying cover images", ct);
            await CopyCoverImagesToBackupDirectory(stagingDirectory, ct);

            await SendProgress(0.35F, "Copying templates images", ct);
            CopyTemplatesToBackupDirectory(stagingDirectory);

            await SendProgress(0.5F, "Copying bookmarks", ct);
            await CopyBookmarksToBackupDirectory(stagingDirectory);

            await SendProgress(0.6F, "Copying Fonts", ct);
            CopyFontsToBackupDirectory(stagingDirectory);

            await SendProgress(0.75F, "Copying themes", ct);
            CopyThemesToBackupDirectory(stagingDirectory);

            try
            {
                await ZipFile.CreateFromDirectoryAsync(stagingDirectory, zipPath, ct);
            }
            catch (AggregateException ex)
            {
                logger.LogError(ex, "There was an issue when archiving library backup");
                return false;
            }

            logger.LogInformation("Database backup completed");
            await SendProgress(1F, "Completed backup", ct);
            return true;
        }
        finally
        {
            DeleteStagingDirectory(stagingDirectory);
            BackupLock.Release();
        }
    }

    private void DeleteStagingDirectory(string? stagingDirectory)
    {
        if (stagingDirectory == null) return;

        // A throw here would skip releasing BackupLock and block every later backup
        try
        {
            directoryService.ClearAndDeleteDirectory(stagingDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the backup staging folder {Folder}; the next backup clears it", stagingDirectory);
        }
    }

    private void CopyLogsToBackupDirectory(string tempDirectory)
    {
        var files = GetLogFiles();
        directoryService.CopyFilesToDirectory(files, directoryService.FileSystem.Path.Join(tempDirectory, "logs"));
    }

    public async Task SnapshotDatabase(string path, CancellationToken ct = default)
    {
        try
        {
            await unitOfWork.DataContext.Database.ExecuteSqlAsync($"VACUUM INTO {path}", ct);

            // VACUUM INTO always writes rollback-journal mode, so a restored WAL server would stay off WAL for good
            if (await unitOfWork.DataContext.Database.GetJournalModeAsync(ct) == DbJournalMode.Wal)
            {
                await SetWalMode(path, ct);
            }

            logger.LogDebug("Database snapshot created at {SnapshotPath}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create database snapshot using VACUUM INTO at {SnapshotPath}", path);
            throw new InvalidOperationException($"Failed to create database snapshot at {path}", ex);
        }
    }

    private static async Task SetWalMode(string databaseFile, CancellationToken ct)
    {
        // Pooling off so the connection really closes, which checkpoints and removes the copy's -wal
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(ct);
    }

    private void CopyTemplatesToBackupDirectory(string tempDirectory)
    {
        directoryService.CopyDirectoryToDirectory(directoryService.TemplateDirectory, directoryService.FileSystem.Path.Join(tempDirectory, "templates"));
    }

    internal async Task CopyCoverImagesToBackupDirectory(string tempDirectory, CancellationToken ct = default)
    {
        var fileSystem = directoryService.FileSystem;
        var rows = await unitOfWork.ImageFileRepository.GetForBackupAsync(ct);

        foreach (var kind in rows.GroupBy(r => r.Kind))
        {
            var folder = await imageFileService.GetImageDirectoryAsync(kind.Key, ct);
            var outputDir = fileSystem.Path.Join(tempDirectory, fileSystem.Path.GetRelativePath(directoryService.ConfigDirectory, folder));
            directoryService.ExistOrCreate(outputDir);

            foreach (var row in kind)
            {
                var source = fileSystem.Path.Join(folder, row.FileName);
                if (!fileSystem.File.Exists(source))
                {
                    logger.LogWarning("{File} is missing, so it is not in the backup", source);
                    continue;
                }

                try
                {
                    fileSystem.File.Copy(source, fileSystem.Path.Join(outputDir, row.FileName));
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Could not copy {File} into the backup", source);
                }
            }
        }
    }

    private async Task CopyBookmarksToBackupDirectory(string tempDirectory)
    {
        var bookmarkDirectory = await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark);

        var outputTempDir = Path.Join(tempDirectory, "bookmarks");
        directoryService.ExistOrCreate(outputTempDir);

        try
        {
            directoryService.CopyDirectoryToDirectory(bookmarkDirectory, outputTempDir);
        }
        catch (IOException)
        {
            // Swallow exception.
        }

        if (!directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private void CopyFontsToBackupDirectory(string tempDirectory)
    {
        var outputTempDir = Path.Join(tempDirectory, "fonts");
        directoryService.ExistOrCreate(outputTempDir);

        try
        {
            directoryService.CopyDirectoryToDirectory(directoryService.EpubFontDirectory, outputTempDir);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Failed to copy fonts to backup directory '{OutputTempDir}'. Fonts will not be included in the backup.", outputTempDir);
        }

        if (!directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private void CopyThemesToBackupDirectory(string tempDirectory)
    {
        var outputTempDir = Path.Join(tempDirectory, "themes");
        directoryService.ExistOrCreate(outputTempDir);

        try
        {
            directoryService.CopyDirectoryToDirectory(directoryService.SiteThemeDirectory, outputTempDir);
        }
        catch (IOException)
        {
            // Swallow exception.
        }

        if (!directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private async Task SendProgress(float progress, string subtitle, CancellationToken ct = default)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.BackupDatabaseProgressEvent(progress, subtitle), ct: ct);
    }

}
