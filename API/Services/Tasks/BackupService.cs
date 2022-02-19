using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.SignalR;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

public interface IBackupService
{
    Task BackupDatabase();
    /// <summary>
    /// Returns a list of full paths of the logs files detailed in <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="maxRollingFiles"></param>
    /// <param name="logFileName"></param>
    /// <returns></returns>
    IEnumerable<string> GetLogFiles(int maxRollingFiles, string logFileName);
}
public class BackupService : IBackupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BackupService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEventHub _eventHub;

    private readonly IList<string> _backupFiles;

    public BackupService(ILogger<BackupService> logger, IUnitOfWork unitOfWork,
        IDirectoryService directoryService, IConfiguration config, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _directoryService = directoryService;
        _eventHub = eventHub;

        var maxRollingFiles = config.GetMaxRollingFiles();
        var loggingSection = config.GetLoggingFileName();
        var files = GetLogFiles(maxRollingFiles, loggingSection);


        _backupFiles = new List<string>()
        {
            "appsettings.json",
            "Hangfire.db", // This is not used atm
            "Hangfire-log.db", // This is not used atm
            "kavita.db",
            "kavita.db-shm", // This wont always be there
            "kavita.db-wal" // This wont always be there
        };

        foreach (var file in files.Select(f => (_directoryService.FileSystem.FileInfo.FromFileName(f)).Name).ToList())
        {
            _backupFiles.Add(file);
        }


    }

    public IEnumerable<string> GetLogFiles(int maxRollingFiles, string logFileName)
    {
        var multipleFileRegex = maxRollingFiles > 0 ? @"\d*" : string.Empty;
        var fi = _directoryService.FileSystem.FileInfo.FromFileName(logFileName);

        var files = maxRollingFiles > 0
            ? _directoryService.GetFiles(_directoryService.LogDirectory,
                $@"{_directoryService.FileSystem.Path.GetFileNameWithoutExtension(fi.Name)}{multipleFileRegex}\.log")
            : new[] {"kavita.log"};
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
            return;
        }

        await SendProgress(0F, "Started backup");

        var dateString = $"{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString()}".Replace("/", "_").Replace(":", "_");
        var zipPath = _directoryService.FileSystem.Path.Join(backupDirectory, $"kavita_backup_{dateString}.zip");

        if (File.Exists(zipPath))
        {
            _logger.LogInformation("{ZipFile} already exists, aborting", zipPath);
            return;
        }

        var tempDirectory = Path.Join(_directoryService.TempDirectory, dateString);
        _directoryService.ExistOrCreate(tempDirectory);
        _directoryService.ClearDirectory(tempDirectory);

        _directoryService.CopyFilesToDirectory(
            _backupFiles.Select(file => _directoryService.FileSystem.Path.Join(_directoryService.ConfigDirectory, file)).ToList(), tempDirectory);

        await SendProgress(0.25F, "Copying core files");

        await CopyCoverImagesToBackupDirectory(tempDirectory);

        await SendProgress(0.5F, "Copying cover images");

        await CopyBookmarksToBackupDirectory(tempDirectory);

        await SendProgress(0.75F, "Copying bookmarks");

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

    private async Task SendProgress(float progress, string subtitle)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.BackupDatabaseProgressEvent(progress, subtitle));
    }

}
