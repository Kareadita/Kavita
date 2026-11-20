using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Helpers;
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
    IImageFileService imageFileService,
    IBackupService backupService)
    : ICleanupService
{
    private const int ImageBatchSize = 500;
    public const string ImageFileMigrationName = "ManualMigrateImageFileBackfillPart2";

    /// <summary>
    /// Cleans up Temp, cache, deleted cover images,  and old database backups
    /// </summary>
    /// <param name="ct"></param>
    public async Task Cleanup(CancellationToken ct = default)
    {
        if (TaskScheduler.IsImageCleanupQueuedOrRunning())
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
        TaskScheduler.QueueImageCleanup(reportSummary: false);
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
    public async Task CleanupImages(bool reportSummary, CancellationToken ct = default)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask<ICleanupService>(s => s.FinishImageFileMigration(CancellationToken.None),
                checkRunningJobs: true))
        {
            logger.LogInformation("[CleanupService] Image cleanup skipped, the image file migration is queued or running");
            return;
        }

        await SendImageProgress(0F, ProgressEventType.Started, ct);

        var run = await StartImageRun(ct);
        await DeleteUnusedImages(run, 0F, 0.2F, ct);
        await ReencodeImages(run, 0.2F, 1F, ct);

        var tally = run.Tally;
        logger.LogInformation(
            "[CleanupService] Deleted {Deleted} unused images and re-encoded {Reencoded}; skipped {Missing} missing and {Unreadable} unreadable",
            tally.Deleted, tally.Reencoded, tally.MissingCount, tally.UnreadableCount);
        await LogSkippedImages(tally, ct);

        if (reportSummary)
        {
            await eventHub.SendMessageAsync(MessageFactory.Info,
                MessageFactory.MediaConversionSummaryEvent(tally.Reencoded, tally.Deleted, tally.BytesSaved,
                    tally.MissingCount, tally.UnreadableCount), ct: ct);
        }

        await SendImageProgress(1F, ProgressEventType.Ended, ct);
    }

    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    public Task FinishImageFileMigration(CancellationToken ct = default)
    {
        return FinishImageFileMigration(Process.GetCurrentProcess().StartTime.ToUniversalTime(), ct);
    }

    /// <param name="sweepBeforeUtc">Only files last written before this are swept, so a scan running now keeps the cover
    /// file it wrote before committing its row</param>
    internal async Task FinishImageFileMigration(DateTime sweepBeforeUtc, CancellationToken ct = default)
    {
        if (await unitOfWork.DataContext.ManualMigrationHistory.AnyAsync(m => m.Name == ImageFileMigrationName, ct)) return;

        if (!await unitOfWork.ImageFileRepository.HasImageFilesNeedingMigrationAsync(ct))
        {
            await CompleteImageFileMigration(ct);
            return;
        }

        logger.LogInformation("[CleanupService] Finishing the image file migration, taking a backup first");
        if (!await backupService.BackupDatabase(ct))
        {
            logger.LogWarning("[CleanupService] Backup failed, so the image file migration changed nothing. It runs again on the next start");
            return;
        }

        await SendImageProgress(0F, ProgressEventType.Started, ct);

        var run = await StartImageRun(ct);
        await RemoveDuplicateKeyedImages(run, ct);
        await DeleteUnusedImages(run, 0F, 0.1F, ct);
        await MigrateImageFiles(run, 0.1F, 0.8F, ct);
        await ReencodeImages(run, 0.8F, 0.95F, ct);
        await SweepUnnamedFiles(run, sweepBeforeUtc, 0.95F, 1F, ct);

        var tally = run.Tally;
        logger.LogInformation(
            "[CleanupService] Image file migration: {Reencoded} re-encoded, {Renamed} renamed, {Deleted} files removed, {BytesSaved} bytes saved, {Missing} missing, {Unreadable} unreadable",
            tally.Reencoded, tally.Renamed, tally.Deleted, tally.BytesSaved, tally.MissingCount, tally.UnreadableCount);
        await LogSkippedImages(tally, ct);

        await CompleteImageFileMigration(ct);

        await eventHub.SendMessageAsync(MessageFactory.Info,
            MessageFactory.ImageMigrationSummaryEvent(tally.Reencoded + tally.Renamed, tally.Deleted, tally.BytesSaved,
                tally.MissingCount, tally.UnreadableCount), ct: ct);
        await SendImageProgress(1F, ProgressEventType.Ended, ct);
    }

    private async Task CompleteImageFileMigration(CancellationToken ct)
    {
        unitOfWork.DataContext.ManualMigrationHistory.Add(new ManualMigrationHistory { Name = ImageFileMigrationName });
        await unitOfWork.CommitAsync(ct);
    }

    private async Task SendImageProgress(float progress, string eventType, CancellationToken ct)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertMediaProgressEvent(progress, eventType), ct: ct);
    }

    private async Task SendStageProgress(float start, float end, string subtitle, int current, int total, CancellationToken ct)
    {
        var done = total == 0 ? 1F : Math.Min(1F, (float) current / total);
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.ConvertMediaProgressEvent(start + (end - start) * done,
                ProgressEventType.Updated, subtitle, current, total), ct: ct);
    }

    private async Task<ImageRun> StartImageRun(CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new ImagePolicy(settings.EncodeMediaAs, settings.CoverImageSize);
        var kinds = Enum.GetValues<ImageKind>().ToList();
        var folders = new Dictionary<ImageKind, string>();

        foreach (var kind in kinds)
        {
            folders[kind] = await imageFileService.GetImageDirectoryAsync(kind, ct);
        }

        return new ImageRun(policy, kinds, folders, new ImageCleanupTally());
    }

    /// <summary>
    /// Keeps one favicon or publisher row per key: the one whose file exists, then the one already named for the target
    /// format, then the newest. Without this, re-encoding host.png to host.webp collides with the host.webp row
    /// </summary>
    private async Task RemoveDuplicateKeyedImages(ImageRun run, CancellationToken ct)
    {
        var duplicates = await unitOfWork.ImageFileRepository.GetKeyedImageFilesWithDuplicateKeysAsync(ct);
        if (duplicates.Count == 0) return;

        var removed = duplicates
            .GroupBy(f => new { f.Kind, f.Key })
            .SelectMany(group =>
            {
                var extension = run.Policy.Format.ForKind(group.Key.Kind).GetExtension();
                var kept = group
                    .OrderByDescending(f => File.Exists(Path.Join(run.Folders[f.Kind], f.FileName)))
                    .ThenByDescending(f => Path.GetExtension(f.FileName).Equals(extension, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(f => f.Id)
                    .First();
                return group.Where(f => f.Id != kept.Id);
            })
            .ToList();

        foreach (var row in removed)
        {
            unitOfWork.ImageFileRepository.Remove(row);
        }

        await unitOfWork.CommitAsync(ct);

        foreach (var row in removed)
        {
            var path = Path.Join(run.Folders[row.Kind], row.FileName);
            var size = GetFileSize(path);
            if (directoryService.DeleteFileQuietly(path))
            {
                run.Tally.Deleted++;
                run.Tally.BytesSaved += size;
            }
        }

        logger.LogInformation("[CleanupService] Removed {Count} favicon and publisher rows that repeated another row's key", removed.Count);
    }

    private async Task DeleteUnusedImages(ImageRun run, float start, float end, CancellationToken ct)
    {
        const string subtitle = "Removing unused images";
        var total = await unitOfWork.ImageFileRepository.CountImageFilesForDeletionAsync(ct);
        var current = 0;
        await SendStageProgress(start, end,subtitle, current, total, ct);

        await foreach (var batch in unitOfWork.ImageFileRepository.GetImageFilesForDeletionAsync(ImageBatchSize, ct))
        {
            foreach (var row in batch)
            {
                unitOfWork.ImageFileRepository.Remove(row);
            }

            await unitOfWork.CommitAsync(ct);

            foreach (var row in batch)
            {
                var path = Path.Join(run.Folders[row.Kind], row.FileName);
                var size = GetFileSize(path);
                if (directoryService.DeleteFileQuietly(path))
                {
                    run.Tally.BytesSaved += size;
                }
            }

            run.Tally.Deleted += batch.Count;
            current += batch.Count;
            await SendStageProgress(start, end,subtitle, current, total, ct);
        }
    }

    private async Task MigrateImageFiles(ImageRun run, float start, float end, CancellationToken ct)
    {
        const string subtitle = "Checking images";
        var total = await unitOfWork.ImageFileRepository.CountImageFilesNeedingMigrationAsync(ct);
        var current = 0;
        await SendStageProgress(start, end,subtitle, current, total, ct);

        foreach (var kind in run.Kinds)
        {
            var folder = run.Folders[kind];
            var format = run.Policy.Format.ForKind(kind);
            var box = ImageProfile.For(kind).GetBox(run.Policy);

            await foreach (var batch in unitOfWork.ImageFileRepository.GetImageFilesNeedingMigrationAsync(kind, ImageBatchSize, ct))
            {
                var replacedNames = new Dictionary<int, string>();
                foreach (var row in batch)
                {
                    var source = Path.Join(folder, row.FileName);
                    if (!File.Exists(source))
                    {
                        run.Tally.Skipped.Add(new SkippedImage(source, kind, row.Id, Unreadable: false));
                        continue;
                    }

                    var fileName = Path.ChangeExtension(row.FileName, format.GetExtension());
                    int width, height;
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(source, ct);
                        var header = ImageHelper.GetImageHeader(bytes);
                        var inLine = header != null && header.Extension == format.GetExtension() &&
                                     (box is not { } b || (header.Width <= b.Width && header.Height <= b.Height));

                        if (inLine)
                        {
                            if (!IsSameName(fileName, row.FileName))
                            {
                                imagePipeline.Copy(source, kind, folder, fileName);
                                run.Tally.Renamed++;
                            }

                            width = header!.Width;
                            height = header.Height;
                        }
                        else
                        {
                            var written = imagePipeline.Reencode(bytes, kind, run.Policy, folder, fileName);
                            run.Tally.BytesSaved += bytes.Length - GetFileSize(Path.Join(folder, fileName));
                            run.Tally.Reencoded++;
                            width = written.Width;
                            height = written.Height;
                        }
                    }
                    catch (Exception ex) when (ex is VipsException or IOException)
                    {
                        run.Tally.Skipped.Add(new SkippedImage(source, kind, row.Id, Unreadable: true));
                        logger.LogDebug(ex, "[CleanupService] Could not read {File}", source);
                        continue;
                    }

                    if (!IsSameName(fileName, row.FileName))
                    {
                        replacedNames[row.Id] = row.FileName;
                    }

                    row.FileName = fileName;
                    row.Format = format;
                    row.Width = width;
                    row.Height = height;
                }

                await CommitBatch(replacedNames, folder, ct);
                current += batch.Count;
                await SendStageProgress(start, end,subtitle, current, total, ct);
            }
        }
    }

    private async Task ReencodeImages(ImageRun run, float start, float end, CancellationToken ct)
    {
        const string subtitle = "Re-encoding images";
        var total = 0;
        foreach (var kind in run.Kinds)
        {
            total += await unitOfWork.ImageFileRepository.CountImageFilesForReencodeAsync(kind, run.Policy.Format.ForKind(kind),
                ImageProfile.For(kind).GetBox(run.Policy), ct);
        }
        var current = 0;
        await SendStageProgress(start, end,subtitle, current, total, ct);

        foreach (var kind in run.Kinds)
        {
            var folder = run.Folders[kind];
            var format = run.Policy.Format.ForKind(kind);
            var box = ImageProfile.For(kind).GetBox(run.Policy);

            await foreach (var batch in unitOfWork.ImageFileRepository.GetImageFilesForReencodeAsync(kind, format, box, ImageBatchSize, ct))
            {
                var replacedNames = new Dictionary<int, string>();
                foreach (var row in batch)
                {
                    var source = Path.Join(folder, row.FileName);
                    if (!File.Exists(source))
                    {
                        run.Tally.Skipped.Add(new SkippedImage(source, kind, row.Id, Unreadable: false));
                        continue;
                    }

                    var sizeBefore = GetFileSize(source);
                    var fileName = Path.ChangeExtension(row.FileName, format.GetExtension());
                    ImageWriteResult written;
                    try
                    {
                        written = imagePipeline.Reencode(source, kind, run.Policy, folder, fileName);
                    }
                    catch (Exception ex) when (ex is VipsException or IOException)
                    {
                        run.Tally.Skipped.Add(new SkippedImage(source, kind, row.Id, Unreadable: true));
                        logger.LogDebug(ex, "[CleanupService] Could not read {File}", source);
                        continue;
                    }

                    run.Tally.BytesSaved += sizeBefore - GetFileSize(Path.Join(folder, fileName));
                    if (!IsSameName(fileName, row.FileName))
                    {
                        replacedNames[row.Id] = row.FileName;
                    }

                    row.FileName = fileName;
                    row.Format = format;
                    row.Width = written.Width;
                    row.Height = written.Height;
                    run.Tally.Reencoded++;
                }

                await CommitBatch(replacedNames, folder, ct);
                current += batch.Count;
                await SendStageProgress(start, end,subtitle, current, total, ct);
            }
        }
    }

    /// <summary>
    /// Commits the batch, then deletes the files it renamed away from. A file is never moved: if the commit failed, the
    /// row would still name the old file
    /// </summary>
    private async Task CommitBatch(Dictionary<int, string> replacedNames, string folder, CancellationToken ct)
    {
        await unitOfWork.CommitAsync(ct);

        foreach (var oldName in replacedNames.Values)
        {
            directoryService.DeleteFileQuietly(Path.Join(folder, oldName));
        }
    }

    private async Task SweepUnnamedFiles(ImageRun run, DateTime sweepBeforeUtc, float start, float end, CancellationToken ct)
    {
        const string subtitle = "Removing leftover files";
        var unnamed = new List<string>();
        foreach (var group in run.Kinds.Where(k => !ImageProfile.For(k).KeepFile).GroupBy(k => run.Folders[k]))
        {
            // Windows matches file names ignoring case, so a case-sensitive set would delete series1.webp whose row says Series1.webp
            var names = new HashSet<string>(await unitOfWork.ImageFileRepository.GetFileNamesAsync(group.ToList(), ct),
                StringComparer.OrdinalIgnoreCase);

            unnamed.AddRange(directoryService.GetFiles(group.Key)
                .Where(path => IsTmpFile(path) || !names.Contains(Path.GetFileName(path))));
        }

        await SendStageProgress(start, end,subtitle, 0, unnamed.Count, ct);

        for (var i = 0; i < unnamed.Count; i++)
        {
            var file = directoryService.FileSystem.FileInfo.New(unnamed[i]);
            if (file.LastWriteTimeUtc < sweepBeforeUtc)
            {
                var size = file.Length;
                if (directoryService.DeleteFileQuietly(file.FullName))
                {
                    run.Tally.Deleted++;
                    run.Tally.BytesSaved += size;
                }
            }

            if ((i + 1) % ImageBatchSize == 0)
            {
                await SendStageProgress(start, end,subtitle, i + 1, unnamed.Count, ct);
            }
        }

        await SendStageProgress(start, end,subtitle, unnamed.Count, unnamed.Count, ct);
    }

    private static bool IsTmpFile(string path) => Path.GetExtension(path).Equals(".tmp", StringComparison.OrdinalIgnoreCase);

    private async Task LogSkippedImages(ImageCleanupTally tally, CancellationToken ct)
    {
        if (tally.Skipped.Count == 0) return;

        var holders = (await unitOfWork.ImageFileRepository.GetAllHoldersAsync(tally.Skipped.Select(s => s.RowId).Distinct().ToList(), ct))
            .ToLookup(h => h.CoverImageFileId!.Value);

        foreach (var skipped in tally.Skipped)
        {
            var heldBy = string.Join(", ", holders[skipped.RowId].Select(h => $"{h.GetType().Name} {h.Id}"));
            logger.LogInformation("[CleanupService] {Reason} image: {File} ({Kind}, {HeldBy})",
                skipped.Unreadable ? "Unreadable" : "Missing", skipped.Path, skipped.Kind, heldBy);
        }
    }

    // Lowercase extensions are what Kavita writes, but on Windows two names differing only by case are the same file,
    // and treating them as different would delete the file just written
    private static bool IsSameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static long GetFileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private sealed record ImageRun(ImagePolicy Policy, IReadOnlyList<ImageKind> Kinds,
        IReadOnlyDictionary<ImageKind, string> Folders, ImageCleanupTally Tally);

    private sealed record SkippedImage(string Path, ImageKind Kind, int RowId, bool Unreadable);
    private sealed class ImageCleanupTally
    {
        public int Deleted { get; set; }
        public int Reencoded { get; set; }
        public int Renamed { get; set; }
        public long BytesSaved { get; set; }
        public List<SkippedImage> Skipped { get; } = [];
        public int MissingCount => Skipped.Count(s => !s.Unreadable);
        public int UnreadableCount => Skipped.Count(s => s.Unreadable);
    }
}
