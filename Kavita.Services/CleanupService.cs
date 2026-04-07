using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

/// <summary>
/// Cleans up after operations on reoccurring basis
/// </summary>
public class CleanupService(
    ILogger<CleanupService> logger,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    IDirectoryService directoryService)
    : ICleanupService
{
    /// <summary>
    /// Cleans up Temp, cache, deleted cover images,  and old database backups
    /// </summary>
    /// <param name="ct"></param>
    [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail, DelaysInSeconds = [120, 300, 300])]
    public async Task Cleanup(CancellationToken ct = default)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask(BookmarkService.Name, "ConvertAllCoverToEncoding", [],
                TaskScheduler.DefaultQueue, true) ||
            TaskScheduler.HasAlreadyEnqueuedTask(BookmarkService.Name, "ConvertAllBookmarkToEncoding", [],
                TaskScheduler.DefaultQueue, true))
        {
            logger.LogInformation("Cleanup put on hold as a media conversion in progress");
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.ErrorEvent("Cleanup", "Cleanup put on hold as a media conversion in progress"), ct: ct);
            return;
        }

        logger.LogInformation("Starting Cleanup");

        // TODO: Why do I have clear temp directory then immediately do it again?
        var cleanupSteps = new List<(Func<CancellationToken, Task>, string)>
        {
            (innerCt => Task.Run(() => directoryService.ClearDirectory(directoryService.TempDirectory), innerCt), "Cleaning temp directory"),
            (CleanupCacheAndTempDirectories, "Cleaning cache and temp directories"),
            (CleanupBackups, "Cleaning old database backups"),
            (ConsolidateProgress, "Consolidating Progress Events"),
            (CleanupMediaErrors, "Consolidating Media Errors"),
            (CleanupDbEntries, "Cleaning abandoned database rows"), // Cleanup DB before removing files linked to DB entries
            (DeleteSeriesCoverImages, "Cleaning deleted series cover images"),
            (DeleteChapterCoverImages, "Cleaning deleted chapter cover images"),
            (innerCt => Task.WhenAll(DeleteTagCoverImages(innerCt), DeleteReadingListCoverImages(innerCt), DeletePersonCoverImages(innerCt)), "Cleaning deleted cover images"),
            (CleanupLogs, "Cleaning old logs"),
            (EnsureChapterProgressIsCapped, "Cleaning progress events that exceed 100%")
        };

        await SendProgress(0F, "Starting cleanup", ct);

        for (var i = 0; i < cleanupSteps.Count; i++)
        {
            var (method, subtitle) = cleanupSteps[i];
            var progress = (float)(i + 1) / (cleanupSteps.Count + 1);

            logger.LogInformation("{Message}", subtitle);
            await method(ct);
            await SendProgress(progress, subtitle, ct);
        }

        await SendProgress(1F, "Cleanup finished", ct);
        logger.LogInformation("Cleanup finished");
    }

    /// <summary>
    /// Cleans up abandon rows in the DB
    /// </summary>
    public async Task CleanupDbEntries(CancellationToken ct = default)
    {
        await unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters(ct);
        await unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated(ct);
        await unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated(ct: ct);
        await unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated(ct);
        await unitOfWork.CollectionTagRepository.RemoveCollectionsWithoutSeries(ct);
        await unitOfWork.ReadingListRepository.RemoveReadingListsWithoutSeries(ct);
    }

    private async Task SendProgress(float progress, string subtitle, CancellationToken ct = default)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CleanupProgressEvent(progress, subtitle), ct: ct);
    }

    /// <summary>
    /// Removes all series images that are not in the database. They must follow <see cref="ImageService.SeriesCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteSeriesCoverImages(CancellationToken ct = default)
    {
        var images = await unitOfWork.SeriesRepository.GetAllCoverImagesAsync(ct);
        var files = directoryService.GetFiles(directoryService.CoverImageDirectory, ImageService.SeriesCoverImageRegex);
        directoryService.DeleteFiles(files.Where(file => !images.Contains(directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all chapter/volume images that are not in the database. They must follow <see cref="ImageService.ChapterCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteChapterCoverImages(CancellationToken ct = default)
    {
        var images = await unitOfWork.ChapterRepository.GetAllCoverImagesAsync(ct);
        var files = directoryService.GetFiles(directoryService.CoverImageDirectory, ImageService.ChapterCoverImageRegex);
        directoryService.DeleteFiles(files.Where(file => !images.Contains(directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all collection tag images that are not in the database. They must follow <see cref="ImageService.CollectionTagCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteTagCoverImages(CancellationToken ct = default)
    {
        var images = await unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync(ct);
        var files = directoryService.GetFiles(directoryService.CoverImageDirectory, ImageService.CollectionTagCoverImageRegex);
        directoryService.DeleteFiles(files.Where(file => !images.Contains(directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all reading list images that are not in the database. They must follow <see cref="ImageService.ReadingListCoverImageRegex"/> filename pattern.
    /// </summary>
    public async Task DeleteReadingListCoverImages(CancellationToken ct = default)
    {
        var images = await unitOfWork.ReadingListRepository.GetAllCoverImagesAsync(ct);
        var files = directoryService.GetFiles(directoryService.CoverImageDirectory, ImageService.ReadingListCoverImageRegex);
        directoryService.DeleteFiles(files.Where(file => !images.Contains(directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Remove all person cover images no longer associated with a person in the database
    /// </summary>
    public async Task DeletePersonCoverImages(CancellationToken ct = default)
    {
        var images = await unitOfWork.PersonRepository.GetAllCoverImagesAsync(ct);
        var files = directoryService.GetFiles(directoryService.CoverImageDirectory, ImageService.PersonCoverImageRegex);
        directoryService.DeleteFiles(files.Where(file => !images.Contains(directoryService.FileSystem.Path.GetFileName(file))));
    }

    /// <summary>
    /// Removes all files and directories in the cache and temp directory
    /// </summary>
    public Task CleanupCacheAndTempDirectories(CancellationToken ct = default)
    {
        logger.LogInformation("Performing cleanup of Cache & Temp directories");
        directoryService.ExistOrCreate(directoryService.CacheDirectory);
        directoryService.ExistOrCreate(directoryService.TempDirectory);

        try
        {
            directoryService.ClearDirectory(directoryService.CacheDirectory);
            directoryService.ClearDirectory(directoryService.TempDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        logger.LogInformation("Cache and temp directory purged");

        return Task.CompletedTask;
    }

    public void CleanupCacheDirectory()
    {
        logger.LogInformation("Performing cleanup of Cache directories");
        directoryService.ExistOrCreate(directoryService.CacheDirectory);

        try
        {
            directoryService.ClearDirectory(directoryService.CacheDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        logger.LogInformation("Cache directory purged");
    }

    /// <summary>
    /// Removes Database backups older than configured total backups. If all backups are older than total backups days, only the latest is kept.
    /// </summary>
    public async Task CleanupBackups(CancellationToken ct = default)
    {
        var dayThreshold = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).TotalBackups;
        logger.LogInformation("Beginning cleanup of Database backups at {Time}", DateTime.Now);
        var backupDirectory =
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory, ct)).Value;
        if (!directoryService.Exists(backupDirectory)) return;

        var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
        var allBackups = directoryService.GetFiles(backupDirectory).ToList();
        var expiredBackups = allBackups.Select(filename => directoryService.FileSystem.FileInfo.New(filename))
            .Where(f => f.CreationTime < deltaTime)
            .ToList();

        if (expiredBackups.Count == allBackups.Count)
        {
            logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
            var toDelete = expiredBackups.OrderByDescending(f => f.CreationTime).ToList();
            directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
        }
        else
        {
            directoryService.DeleteFiles(expiredBackups.Select(f => f.FullName));
        }
        logger.LogInformation("Finished cleanup of Database backups at {Time}", DateTime.Now);
    }

    /// <summary>
    /// Find any progress events that have duplicate, find the highest page read event, then copy over information from that and delete others, to leave one.
    /// </summary>
    public async Task ConsolidateProgress(CancellationToken ct = default)
    {
        logger.LogInformation("Consolidating Progress Events");
        // AppUserProgress
        var allProgress = await unitOfWork.AppUserProgressRepository.GetAllProgress(ct);

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
                unitOfWork.AppUserProgressRepository.Remove(duplicate);
            }
        }

        // Save changes
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Scans through Media Error and removes any entries that have been fixed and are within the DB (proper files where wordcount/pagecount > 0)
    /// </summary>
    public async Task CleanupMediaErrors(CancellationToken ct = default)
    {
        try
        {
            List<string> errorStrings = ["This archive cannot be read or not supported", "File format not supported"];
            var mediaErrors = await unitOfWork.MediaErrorRepository.GetAllErrorsAsync(errorStrings, ct);
            logger.LogInformation("Beginning consolidation of {Count} Media Errors", mediaErrors.Count);

            var pathToErrorMap = mediaErrors
                .GroupBy(me => Parser.NormalizePath(me.FilePath))
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList() // The same file can be duplicated (rare issue when network drives die out midscan)
                );

            var normalizedPaths = pathToErrorMap.Keys.ToList();

            // Find all files that are valid
            var validFiles = await unitOfWork.DataContext.MangaFile
                .Where(f => normalizedPaths.Contains(f.FilePath) && f.Pages > 0)
                .Select(f => f.FilePath)
                .ToListAsync(cancellationToken: ct);

            var removalCount = 0;
            foreach (var validFilePath in validFiles)
            {
                if (!pathToErrorMap.TryGetValue(validFilePath, out var mediaError)) continue;

                unitOfWork.MediaErrorRepository.Remove(mediaError);
                removalCount++;
            }

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation("Finished consolidation of {Count} Media Errors, Removed: {RemovalCount}",
                mediaErrors.Count, removalCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception consolidating media errors");
        }
    }

    public async Task CleanupLogs(CancellationToken ct = default)
    {
        logger.LogInformation("Performing cleanup of logs directory");
        var dayThreshold = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).TotalLogs;
        var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
        var allLogs = directoryService.GetFiles(directoryService.LogDirectory).ToList();
        var expiredLogs = allLogs.Select(filename => directoryService.FileSystem.FileInfo.New(filename))
            .Where(f => f.CreationTime < deltaTime)
            .ToList();

        if (expiredLogs.Count == allLogs.Count)
        {
            logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
            var toDelete = expiredLogs.OrderBy(f => f.CreationTime).ToList();
            directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
        }
        else
        {
            directoryService.DeleteFiles(expiredLogs.Select(f => f.FullName));
        }
        logger.LogInformation("Finished cleanup of logs at {Time}", DateTime.Now);
    }

    public void CleanupTemp()
    {
        logger.LogInformation("Performing cleanup of Temp directory");
        directoryService.ExistOrCreate(directoryService.TempDirectory);

        try
        {
            directoryService.ClearDirectory(directoryService.TempDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
        }

        logger.LogInformation("Temp directory purged");
    }

    /// <summary>
    /// Ensures that each chapter's progress (pages read) is capped at the total pages. This can get out of sync when a chapter is replaced after being read with one with lower page count.
    /// </summary>
    /// <returns></returns>
    public async Task EnsureChapterProgressIsCapped(CancellationToken ct = default)
    {
        logger.LogInformation("Cleaning up any progress rows that exceed chapter page count");
        await unitOfWork.AppUserProgressRepository.UpdateAllProgressThatAreMoreThanChapterPages(ct);
        logger.LogInformation("Cleaning up any progress rows that exceed chapter page count - complete");
    }

    /// <summary>
    /// This does not cleanup any Series that are not Completed or Cancelled
    /// </summary>
    public async Task CleanupWantToRead(CancellationToken ct = default)
    {
        logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list");

        var libraryIds = (await unitOfWork.LibraryRepository.GetLibrariesAsync(ct: ct)).Select(l => l.Id).ToList();
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
        foreach (var user in await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead, ct: ct))
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(0, user.Id, new UserParams(), filter);
            var seriesIds = series.Select(s => s.Id).ToList();
            if (seriesIds.Count == 0) continue;

            user.WantToRead ??= new List<AppUserWantToRead>();
            user.WantToRead = user.WantToRead.Where(s => !seriesIds.Contains(s.SeriesId)).ToList();
            unitOfWork.UserRepository.Update(user);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(ct);
        }

        logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list, completed");
    }
}
