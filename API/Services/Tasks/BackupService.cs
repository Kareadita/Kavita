using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.Logging;
using API.SignalR;
using Hangfire;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
#nullable enable

public interface IBackupService
{
    Task BackupDatabase();
    /// <summary>
    /// Returns a list of all log files for Kavita
    /// </summary>
    /// <param name="rollFiles">If file rolling is enabled. Defaults to True.</param>
    /// <returns></returns>
    IEnumerable<string> GetLogFiles(bool rollFiles = LogLevelOptions.LogRollingEnabled);
}
public class BackupService : IBackupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BackupService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEventHub _eventHub;

    private readonly IList<string> _backupFiles;

    public BackupService(ILogger<BackupService> logger, IUnitOfWork unitOfWork,
        IDirectoryService directoryService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _directoryService = directoryService;
        _eventHub = eventHub;

        _backupFiles = new List<string>()
        {
            "appsettings.json",
            "kavita.db",
            "kavita.db-shm", // This wont always be there
            "kavita.db-wal" // This wont always be there
        };
    }

    /// <summary>
    /// Returns a list of all log files for Kavita
    /// </summary>
    /// <param name="rollFiles">If file rolling is enabled. Defaults to True.</param>
    /// <returns></returns>
    public IEnumerable<string> GetLogFiles(bool rollFiles = LogLevelOptions.LogRollingEnabled)
    {
        var multipleFileRegex = rollFiles ? @"\d*" : string.Empty;
        var fi = _directoryService.FileSystem.FileInfo.New(LogLevelOptions.LogFile);

        var files = rollFiles
            ? _directoryService.GetFiles(_directoryService.LogDirectory,
                $@"{_directoryService.FileSystem.Path.GetFileNameWithoutExtension(fi.Name)}{multipleFileRegex}\.log")
            : new[] {_directoryService.FileSystem.Path.Join(_directoryService.LogDirectory, "kavita.log")};
        return files;
    }

    /// <summary>
    /// Will backup anything that needs to be backed up. This includes logs, setting files, bare minimum cover images (just locked and first cover).
    /// </summary>
    [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task BackupDatabase()
    {
        _logger.LogInformation("Beginning backup of Database at {BackupTime}", DateTime.Now);
        var backupDirectory = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Value;

        _logger.LogDebug("Backing up to {BackupDirectory}", backupDirectory);
        if (!_directoryService.ExistOrCreate(backupDirectory))
        {
            _logger.LogCritical("Could not write to {BackupDirectory}; aborting backup", backupDirectory);
            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent("Backup Service Error",$"Could not write to {backupDirectory}; aborting backup"));
            return;
        }

        await SendProgress(0F, "Started backup");
        await SendProgress(0.1F, "Copying core files");

        var dateString = $"{DateTime.UtcNow.ToShortDateString()}_{DateTime.UtcNow.ToLongTimeString()}".Replace("/", "_").Replace(":", "_");
        var zipPath = _directoryService.FileSystem.Path.Join(backupDirectory, $"kavita_backup_{dateString}_v{BuildInfo.Version}.zip");

        if (File.Exists(zipPath))
        {
            _logger.LogCritical("{ZipFile} already exists, aborting", zipPath);
            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent("Backup Service Error",$"{zipPath} already exists, aborting"));
            return;
        }

        var tempDirectory = Path.Join(_directoryService.TempDirectory, dateString);
        _directoryService.ExistOrCreate(tempDirectory);
        _directoryService.ClearDirectory(tempDirectory);

        await SendProgress(0.1F, "Copying config files");
        _directoryService.CopyFilesToDirectory(
            _backupFiles.Select(file => _directoryService.FileSystem.Path.Join(_directoryService.ConfigDirectory, file)), tempDirectory);

        // Copy any csv's as those are used for manual migrations
        _directoryService.CopyFilesToDirectory(
            _directoryService.GetFilesWithCertainExtensions(_directoryService.ConfigDirectory, @"\.csv"), tempDirectory);

        await SendProgress(0.2F, "Copying logs");
        CopyLogsToBackupDirectory(tempDirectory);

        await SendProgress(0.25F, "Copying cover images");
        await CopyCoverImagesToBackupDirectory(tempDirectory);

        await SendProgress(0.35F, "Copying templates images");
        CopyTemplatesToBackupDirectory(tempDirectory);

        await SendProgress(0.5F, "Copying bookmarks");
        await CopyBookmarksToBackupDirectory(tempDirectory);

        await SendProgress(0.75F, "Copying themes");
        CopyThemesToBackupDirectory(tempDirectory);

        await SendProgress(0.85F, "Copying favicons");
        CopyFaviconsToBackupDirectory(tempDirectory);

        try
        {
            ZipFile.CreateFromDirectory(tempDirectory, zipPath);
        }
        catch (AggregateException ex)
        {
            _logger.LogError(ex, "There was an issue when archiving library backup");
        }

        _directoryService.ClearAndDeleteDirectory(tempDirectory);
        _logger.LogInformation("Database backup completed");
        await SendProgress(1F, "Completed backup");
    }

    private void CopyLogsToBackupDirectory(string tempDirectory)
    {
        var files = GetLogFiles();
        _directoryService.CopyFilesToDirectory(files, _directoryService.FileSystem.Path.Join(tempDirectory, "logs"));
    }

    private void CopyFaviconsToBackupDirectory(string tempDirectory)
    {
        _directoryService.CopyDirectoryToDirectory(_directoryService.FaviconDirectory, _directoryService.FileSystem.Path.Join(tempDirectory, "favicons"));
    }

    private void CopyTemplatesToBackupDirectory(string tempDirectory)
    {
        _directoryService.CopyDirectoryToDirectory(_directoryService.TemplateDirectory, _directoryService.FileSystem.Path.Join(tempDirectory, "templates"));
    }

    private async Task CopyCoverImagesToBackupDirectory(string tempDirectory)
    {
        var outputTempDir = Path.Join(tempDirectory, "covers");
        _directoryService.ExistOrCreate(outputTempDir);

        try
        {
            var seriesImages = await _unitOfWork.SeriesRepository.GetLockedCoverImagesAsync();
            _directoryService.CopyFilesToDirectory(
                seriesImages.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);

            var collectionTags = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
            _directoryService.CopyFilesToDirectory(
                collectionTags.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);

            var chapterImages = await _unitOfWork.ChapterRepository.GetCoverImagesForLockedChaptersAsync();
            _directoryService.CopyFilesToDirectory(
                chapterImages.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);

            var volumeImages = await _unitOfWork.VolumeRepository.GetCoverImagesForLockedVolumesAsync();
            _directoryService.CopyFilesToDirectory(
                volumeImages.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);

            var libraryImages = await _unitOfWork.LibraryRepository.GetAllCoverImagesAsync();
            _directoryService.CopyFilesToDirectory(
                libraryImages.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);

            var readingListImages = await _unitOfWork.ReadingListRepository.GetAllCoverImagesAsync();
            _directoryService.CopyFilesToDirectory(
                readingListImages.Select(s => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, s)), outputTempDir);
        }
        catch (IOException)
        {
            // Swallow exception. This can be a duplicate cover being copied as chapter and volumes can share same file.
        }

        if (!_directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            _directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private async Task CopyBookmarksToBackupDirectory(string tempDirectory)
    {
        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;

        var outputTempDir = Path.Join(tempDirectory, "bookmarks");
        _directoryService.ExistOrCreate(outputTempDir);

        try
        {
            _directoryService.CopyDirectoryToDirectory(bookmarkDirectory, outputTempDir);
        }
        catch (IOException)
        {
            // Swallow exception.
        }

        if (!_directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            _directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private void CopyThemesToBackupDirectory(string tempDirectory)
    {
        var outputTempDir = Path.Join(tempDirectory, "themes");
        _directoryService.ExistOrCreate(outputTempDir);

        try
        {
            _directoryService.CopyDirectoryToDirectory(_directoryService.SiteThemeDirectory, outputTempDir);
        }
        catch (IOException)
        {
            // Swallow exception.
        }

        if (!_directoryService.GetFiles(outputTempDir, searchOption: SearchOption.AllDirectories).Any())
        {
            _directoryService.ClearAndDeleteDirectory(outputTempDir);
        }
    }

    private async Task SendProgress(float progress, string subtitle)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.BackupDatabaseProgressEvent(progress, subtitle));
    }

}
