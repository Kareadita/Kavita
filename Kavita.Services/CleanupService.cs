using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VipsException = NetVips.VipsException;

namespace Kavita.Services;

/// <summary>
/// Cleans up after operations on reoccurring basis
/// </summary>
public class CleanupService(
    ILogger<CleanupService> logger,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    IDirectoryService directoryService,
    IImagePipeline imagePipeline,
    IImageFileService imageFileService)
    : ICleanupService
{
    private const int ImageBatchSize = 500;

    /// <summary>
    /// Cleans up Temp, cache, deleted cover images,  and old database backups
    /// </summary>
    /// <param name="ct"></param>
    public async Task Cleanup(CancellationToken ct = default)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask<ICleanupService>(s => s.CleanupImages(CancellationToken.None),
                checkRunningJobs: true))
        {
            logger.LogInformation("Cleanup put on hold as an image cleanup is in progress");
            await eventHub.SendMessageAsync(MessageFactory.Info,
                MessageFactory.CleanupOnHoldEvent(), ct: ct);
            return;
        }

        logger.LogInformation("Starting Cleanup");

        var cleanupSteps = new List<(Func<CancellationToken, Task>, string)>
        {
            (CleanupCacheAndTempDirectories, "Cleaning cache and temp directories"),
            (CleanupBackups, "Cleaning old database backups"),
            (CleanupDebugExports, "Cleaning old debug exports"),
            (ConsolidateProgress, "Consolidating Progress Events"),
            (CleanupDbEntries, "Cleaning abandoned database rows"),
            (CleanupLogs, "Cleaning old logs"),
            (EnsureChapterProgressIsCapped, "Cleaning progress events that exceed 100%"),
            // Last, so the image rows of anything removed above are already unreferenced
            (QueueImageCleanup, "Queueing image cleanup")
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

    private static Task QueueImageCleanup(CancellationToken ct)
    {
        TaskScheduler.QueueImageCleanup();
        return Task.CompletedTask;
    }

    private async Task SendProgress(float progress, string subtitle, CancellationToken ct = default)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.CleanupProgressEvent(progress, subtitle), ct: ct);
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
    /// Deletes each debug export whose newest file is over 24 hours old, finished, cut off by a restart or never downloaded
    /// </summary>
    public Task CleanupDebugExports(CancellationToken ct = default)
    {
        var fileSystem = directoryService.FileSystem;
        if (!directoryService.Exists(directoryService.DebugExportDirectory)) return Task.CompletedTask;

        var cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (var folder in fileSystem.Directory.EnumerateDirectories(directoryService.DebugExportDirectory).ToList())
        {
            // The folder's creation time keeps an export started a moment ago, still empty, from being deleted
            var newest = fileSystem.Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(f => fileSystem.File.GetLastWriteTimeUtc(f))
                .Append(fileSystem.Directory.GetCreationTimeUtc(folder))
                .Max();

            if (newest >= cutoff) continue;

            try
            {
                fileSystem.Directory.Delete(folder, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete the debug export {Folder}", folder);
            }
        }

        return Task.CompletedTask;
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
            logger.LogInformation("All logs are older than {Threshold} days. Removing all but the last log", dayThreshold);
            var toDelete = expiredLogs.OrderBy(f => f.CreationTime).ToList();
            directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
        }
        else
        {
            directoryService.DeleteFiles(expiredLogs.Select(f => f.FullName));
        }
        logger.LogInformation("Finished cleanup of logs at {Time}", DateTime.Now);
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
    /// This does not clean up any Series that are not Completed or Cancelled
    /// </summary>
    public async Task CleanupWantToRead(CancellationToken ct = default)
    {
        logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list");

        var filter = new SeriesFilterV2Dto()
        {
            Combination = FilterCombination.And,
            Statements =
            [
                new SeriesFilterStatementDto()
                {
                    Comparison = FilterComparison.Contains,
                    Field = SeriesFilterField.PublicationStatus,
                    Value = $"{(int) PublicationStatus.Completed},{(int) PublicationStatus.Cancelled}"
                },
                new SeriesFilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = SeriesFilterField.ReadProgress,
                    Value = "100"
                },
                new SeriesFilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = SeriesFilterField.WantToRead,
                    Value = "true"
                }
            ]
        };

        foreach (var user in await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead, ct: ct))
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(user.Id,
                new UserParams() { PageSize = int.MaxValue }, filter, ct: ct);
            var seriesIds = series.Select(s => s.Id).ToList();
            if (seriesIds.Count == 0) continue;

            user.WantToRead ??= [];
            user.WantToRead = user.WantToRead.Where(s => !seriesIds.Contains(s.SeriesId)).ToList();
            unitOfWork.UserRepository.Update(user);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(ct);
        }

        logger.LogInformation("Performing cleanup of Series that are Completed and have been fully read that are in Want To Read list, completed");
    }

    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public async Task CleanupImages(CancellationToken ct = default)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(0F, ProgressEventType.Started), ct: ct);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new ImagePolicy(settings.EncodeMediaAs, settings.CoverImageSize);
        var kinds = Enum.GetValues<ImageKind>()
            .Where(k => k != ImageKind.Bookmark)
            .ToList();
        var folders = new Dictionary<ImageKind, string>();
        var tally = new ImageCleanupTally();

        foreach (var kind in kinds)
        {
            folders[kind] = await imageFileService.GetImageDirectoryAsync(kind, ct);
        }

        await DeleteUnusedImages(folders, tally, ct);

        foreach (var kind in kinds)
        {
            await ReencodeImages(kind, folders[kind], policy, tally, ct);
        }

        logger.LogInformation(
            "[CleanupService] Deleted {Deleted} unused images and re-encoded {Reencoded}; skipped {Missing} missing and {Unreadable} unreadable",
            tally.Deleted, tally.Reencoded, tally.Missing, tally.Unreadable);

        // TODO: We may want to pass extra information into this event
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertCoverProgressEvent(1F, ProgressEventType.Ended), ct: ct);
    }

    private async Task DeleteUnusedImages(Dictionary<ImageKind, string> folders, ImageCleanupTally tally, CancellationToken ct)
    {
        await foreach (var batch in unitOfWork.ImageFileRepository.GetImageFilesForDeletionAsync(ImageBatchSize, ct))
        {
            foreach (var row in batch)
            {
                unitOfWork.ImageFileRepository.Remove(row);
            }

            await unitOfWork.CommitAsync(ct);

            foreach (var row in batch)
            {
                DeleteQuietly(Path.Join(folders[row.Kind], row.FileName));
            }

            tally.Deleted += batch.Count;
        }
    }

    private async Task ReencodeImages(ImageKind kind, string folder, ImagePolicy policy, ImageCleanupTally tally,
        CancellationToken ct)
    {
        var format = policy.Format.ForKind(kind);
        var box = ImageProfile.For(kind).GetBox(policy);

        await foreach (var batch in unitOfWork.ImageFileRepository.GetImageFilesForReencodeAsync(kind, format, box, ImageBatchSize, ct))
        {
            var replacedNames = new Dictionary<int, string>();
            foreach (var row in batch)
            {
                var source = Path.Join(folder, row.FileName);
                if (!File.Exists(source))
                {
                    tally.Missing++;
                    logger.LogInformation("[CleanupService] Missing image: {File} ({Kind}, row {Id})", source, kind, row.Id);
                    continue;
                }

                var fileName = Path.ChangeExtension(row.FileName, format.GetExtension());
                ImageWriteResult written;
                try
                {
                    written = imagePipeline.Reencode(source, kind, policy, folder, fileName);
                }
                catch (Exception ex) when (ex is VipsException or IOException)
                {
                    tally.Unreadable++;
                    logger.LogInformation(ex, "[CleanupService] Unreadable image: {File} ({Kind}, row {Id})", source, kind, row.Id);
                    continue;
                }

                if (!string.Equals(fileName, row.FileName, StringComparison.Ordinal))
                {
                    replacedNames[row.Id] = row.FileName;
                }

                row.FileName = fileName;
                row.Format = format;
                row.Width = written.Width;
                row.Height = written.Height;
                tally.Reencoded++;
            }

            if (replacedNames.Count > 0)
            {
                await SyncLegacyCoverNames(batch.Where(r => replacedNames.ContainsKey(r.Id)).ToList(), ct);
            }

            await unitOfWork.CommitAsync(ct);

            foreach (var oldName in replacedNames.Values)
            {
                DeleteQuietly(Path.Join(folder, oldName));
            }
        }
    }

    // Workaround until every reader uses CoverImageFile, keeps the legacy CoverImage string pointing at the new file
    private async Task SyncLegacyCoverNames(List<ImageFile> renamed, CancellationToken ct)
    {
        var newNames = renamed.ToDictionary(r => r.Id, r => r.FileName);
        var holders = await unitOfWork.ImageFileRepository.GetAllHoldersAsync(newNames.Keys.ToList(), ct);

#pragma warning disable CS0618
        foreach (var holder in holders)
        {
            if (holder is IHasCoverImage legacy)
            {
                legacy.CoverImage = newNames[holder.CoverImageFileId!.Value];
            }
        }
#pragma warning restore CS0618
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "[CleanupService] Could not delete {File}", path);
        }
    }

    private sealed class ImageCleanupTally
    {
        public int Deleted { get; set; }
        public int Reencoded { get; set; }
        public int Missing { get; set; }
        public int Unreadable { get; set; }
    }
}
