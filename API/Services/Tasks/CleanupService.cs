using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
#nullable enable

public interface ICleanupService
{
    Task Cleanup();
    Task CleanupDbEntries();
    void CleanupCacheAndTempDirectories();
    void CleanupCacheDirectory();
    Task DeleteSeriesCoverImages();
    Task DeleteChapterCoverImages();
    Task DeleteTagCoverImages();
    Task CleanupBackups();
    Task CleanupLogs();
    void CleanupTemp();
    Task EnsureChapterProgressIsCapped();
    /// <summary>
    /// Responsible to remove Series from Want To Read when user's have fully read the series and the series has Publication Status of Completed or Cancelled.
    /// </summary>
    /// <returns></returns>
    Task CleanupWantToRead();

    Task ConsolidateProgress();
}
/// <summary>
/// Cleans up after operations on reoccurring basis
/// </summary>
public class CleanupService : ICleanupService
{
    private readonly ILogger<CleanupService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;

    public CleanupService(ILogger<CleanupService> logger,
        IUnitOfWork unitOfWork, IEventHub eventHub,
        IDirectoryService directoryService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _directoryService = directoryService;
    }


    /// <summary>
    /// Cleans up Temp, cache, deleted cover images,  and old database backups
    /// </summary>
    [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Cleanup()
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask(BookmarkService.Name, "ConvertAllCoverToEncoding", Array.Empty<object>(),
                TaskScheduler.DefaultQueue, true) ||
            TaskScheduler.HasAlreadyEnqueuedTask(BookmarkService.Name, "ConvertAllBookmarkToEncoding", Array.Empty<object>(),
                TaskScheduler.DefaultQueue, true))
        {
            _logger.LogInformation("Cleanup put on hold as a media conversion in progress");
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ErrorEvent("Cleanup", "Cleanup put on hold as a media conversion in progress"));
            return;
        }

        _logger.LogInformation("Starting Cleanup");
        await SendProgress(0F, "Starting cleanup");

        _logger.LogInformation("Cleaning temp directory");
        _directoryService.ClearDirectory(_directoryService.TempDirectory);

        await SendProgress(0.1F, "Cleaning temp directory");
        CleanupCacheAndTempDirectories();

        await SendProgress(0.25F, "Cleaning old database backups");
        _logger.LogInformation("Cleaning old database backups");
        await CleanupBackups();

        await SendProgress(0.35F, "Consolidating Progress Events");
        _logger.LogInformation("Consolidating Progress Events");
        await ConsolidateProgress();

        await SendProgress(0.50F, "Cleaning deleted cover images");
        _logger.LogInformation("Cleaning deleted cover images");
        await DeleteSeriesCoverImages();
        await SendProgress(0.6F, "Cleaning deleted cover images");
        await DeleteChapterCoverImages();
        await SendProgress(0.7F, "Cleaning deleted cover images");
        await DeleteTagCoverImages();
        await DeleteReadingListCoverImages();
        await SendProgress(0.8F, "Cleaning old logs");
        await CleanupLogs();
        await SendProgress(0.9F, "Cleaning progress events that exceed 100%");
        await EnsureChapterProgressIsCapped();
        await SendProgress(0.95F, "Cleaning abandoned database rows");
        await CleanupDbEntries();
        await SendProgress(1F, "Cleanup finished");
        _logger.LogInformation("Cleanup finished");
    }

    /// <summary>
    /// Cleans up abandon rows in the DB
    /// </summary>
    public async Task CleanupDbEntries()
    {
        await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
        await _unitOfWork.CollectionTagRepository.RemoveCollectionsWithoutSeries();
        await _unitOfWork.ReadingListRepository.RemoveReadingListsWithoutSeries();
    }

    private async Task SendProgress(float progress, string subtitle)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CleanupProgressEvent(progress, subtitle));
    }

    /// <summary>
    /// Removes all series images that are not in the database. They must follow <see cref="ImageService.SeriesCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteSeriesCoverImages()
    {
        var images = await _unitOfWork.SeriesRepository.GetAllCoverImagesAsync();
        var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.SeriesCoverImageRegex);
        _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all chapter/volume images that are not in the database. They must follow <see cref="ImageService.ChapterCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteChapterCoverImages()
    {
        var images = await _unitOfWork.ChapterRepository.GetAllCoverImagesAsync();
        var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.ChapterCoverImageRegex);
        _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all collection tag images that are not in the database. They must follow <see cref="ImageService.CollectionTagCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteTagCoverImages()
    {
        var images = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
        var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.CollectionTagCoverImageRegex);
        _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all reading list images that are not in the database. They must follow <see cref="ImageService.ReadingListCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteReadingListCoverImages()
    {
        var images = await _unitOfWork.ReadingListRepository.GetAllCoverImagesAsync();
        var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.ReadingListCoverImageRegex);
        _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all files and directories in the cache and temp directory
    /// </summary>
    public void CleanupCacheAndTempDirectories()
    {
        _logger.LogInformation("Performing cleanup of Cache & Temp directories");
        _directoryService.ExistOrCreate(_directoryService.CacheDirectory);
        _directoryService.ExistOrCreate(_directoryService.TempDirectory);

        try
        {
            _directoryService.ClearDirectory(_directoryService.CacheDirectory);
            _directoryService.ClearDirectory(_directoryService.TempDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        _logger.LogInformation("Cache and temp directory purged");
    }

    public void CleanupCacheDirectory()
    {
        _logger.LogInformation("Performing cleanup of Cache directories");
        _directoryService.ExistOrCreate(_directoryService.CacheDirectory);

        try
        {
            _directoryService.ClearDirectory(_directoryService.CacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        _logger.LogInformation("Cache directory purged");
    }

    /// <summary>
    /// Removes Database backups older than configured total backups. If all backups are older than total backups days, only the latest is kept.
    /// </summary>
    public async Task CleanupBackups()
    {
        var dayThreshold = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).TotalBackups;
        _logger.LogInformation("Beginning cleanup of Database backups at {Time}", DateTime.Now);
        var backupDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Value;
        if (!_directoryService.Exists(backupDirectory)) return;

        var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
        var allBackups = _directoryService.GetFiles(backupDirectory).ToList();
        var expiredBackups = allBackups.Select(filename => _directoryService.FileSystem.FileInfo.New(filename))
            .Where(f => f.CreationTime < deltaTime)
            .ToList();

        if (expiredBackups.Count == allBackups.Count)
        {
            _logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
            var toDelete = expiredBackups.OrderByDescending(f => f.CreationTime).ToList();
            _directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
        }
        else
        {
            _directoryService.DeleteFiles(expiredBackups.Select(f => f.FullName));
        }
        _logger.LogInformation("Finished cleanup of Database backups at {Time}", DateTime.Now);
    }

    /// <summary>
    /// Find any progress events that have duplicate, find the highest page read event, then copy over information from that and delete others, to leave one.
    /// </summary>
    public async Task ConsolidateProgress()
    {
        // AppUserProgress
        var allProgress = await _unitOfWork.AppUserProgressRepository.GetAllProgress();

        // Group by the unique identifiers that would make a progress entry unique
        var duplicateGroups = allProgress
            .GroupBy(p => new
            {
                p.AppUserId,
                p.ChapterId,
            })
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            // Find the entry with the highest pages read
            var highestProgress = group
                .OrderByDescending(p => p.PagesRead)
                .ThenByDescending(p => p.LastModifiedUtc)
                .First();

            // Get the duplicate entries to remove (all except the highest progress)
            var duplicatesToRemove = group
                .Where(p => p.Id != highestProgress.Id)
                .ToList();

            // Copy over any non-null BookScrollId if the highest progress entry doesn't have one
            if (string.IsNullOrEmpty(highestProgress.BookScrollId))
            {
                var firstValidScrollId = duplicatesToRemove
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p.BookScrollId))
                    ?.BookScrollId;

                if (firstValidScrollId != null)
                {
                    highestProgress.BookScrollId = firstValidScrollId;
                    highestProgress.MarkModified();
                }
            }

            // Remove the duplicates
            foreach (var duplicate in duplicatesToRemove)
            {
                _unitOfWork.AppUserProgressRepository.Remove(duplicate);
            }
        }

        // Save changes
        await _unitOfWork.CommitAsync();
    }

    public async Task CleanupLogs()
    {
        _logger.LogInformation("Performing cleanup of logs directory");
        var dayThreshold = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).TotalLogs;
        var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
        var allLogs = _directoryService.GetFiles(_directoryService.LogDirectory).ToList();
        var expiredLogs = allLogs.Select(filename => _directoryService.FileSystem.FileInfo.New(filename))
            .Where(f => f.CreationTime < deltaTime)
            .ToList();

        if (expiredLogs.Count == allLogs.Count)
        {
            _logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
            var toDelete = expiredLogs.OrderBy(f => f.CreationTime).ToList();
            _directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
        }
        else
        {
            _directoryService.DeleteFiles(expiredLogs.Select(f => f.FullName));
        }
        _logger.LogInformation("Finished cleanup of logs at {Time}", DateTime.Now);
    }

    public void CleanupTemp()
    {
        _logger.LogInformation("Performing cleanup of Temp directory");
        _directoryService.ExistOrCreate(_directoryService.TempDirectory);

        try
        {
            _directoryService.ClearDirectory(_directoryService.TempDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        _logger.LogInformation("Temp directory purged");
    }

    /// <summary>
    /// Ensures that each chapter's progress (pages read) is capped at the total pages. This can get out of sync when a chapter is replaced after being read with one with lower page count.
    /// </summary>
    /// <returns></returns>
    public async Task EnsureChapterProgressIsCapped()
    {
        _logger.LogInformation("Cleaning up any progress rows that exceed chapter page count");
        await _unitOfWork.AppUserProgressRepository.UpdateAllProgressThatAreMoreThanChapterPages();
        _logger.LogInformation("Cleaning up any progress rows that exceed chapter page count - complete");
    }

    /// <summary>
    /// This does not cleanup any Series that are not Completed or Cancelled
    /// </summary>
    public async Task CleanupWantToRead()
    {
        _logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list");

        var libraryIds = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).Select(l => l.Id).ToList();
        var filter = new FilterDto()
        {
            PublicationStatus = new List<PublicationStatus>()
            {
                PublicationStatus.Completed,
                PublicationStatus.Cancelled
            },
            Libraries = libraryIds,
            ReadStatus = new ReadStatus()
            {
                Read = true,
                InProgress = false,
                NotRead = false
            }
        };
        foreach (var user in await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead))
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(0, user.Id, new UserParams(), filter);
            var seriesIds = series.Select(s => s.Id).ToList();
            if (seriesIds.Count == 0) continue;

            user.WantToRead ??= new List<AppUserWantToRead>();
            user.WantToRead = user.WantToRead.Where(s => !seriesIds.Contains(s.SeriesId)).ToList();
            _unitOfWork.UserRepository.Update(user);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        _logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list, completed");
    }
}
