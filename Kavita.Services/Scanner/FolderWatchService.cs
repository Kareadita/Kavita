using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Picks watch or poll for each library folder, drives <see cref="INativeFileWatcherService"/> and
/// <see cref="IFolderPollerService"/>, and hands every change they find to the scanner
/// </summary>
public class FolderWatchService(IUnitOfWork unitOfWork, ILogger<FolderWatchService> logger, ITaskScheduler taskScheduler,
    IMountDetectorService mountDetector, INativeFileWatcherService nativeFileWatcher, IFolderPollerService folderPoller)
    : IFolderWatchService
{
    /// <summary>
    /// Covers the poll and the watch apply together, so two restarts at once never leave a folder both polled and watched
    /// </summary>
    private static readonly SemaphoreSlim ApplyLock = new(1, 1);

    /// <summary>
    /// Each folder's last logged watch or poll line, so a restart after every library save does not repeat it
    /// </summary>
    private static readonly Dictionary<string, string> LoggedChoices = new();

    private sealed record FolderChoice(string Folder, MountInfo? Mount, bool Poll);

    public async Task StartWatching()
    {
        var retryIn = await StartWatchersAsync();
        if (retryIn != null)
        {
            BackgroundJob.Schedule(() => RestartWatching(), retryIn.Value);
        }
    }

    /// <returns>When to try again, if a library folder cannot be reached and no retry is pending yet</returns>
    internal async Task<TimeSpan?> StartWatchersAsync()
    {
        if (!(await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableFolderWatching)
        {
            logger.LogInformation("Folder watching is disabled at the server level, thus ignoring any requests to create folder watching");
            StopWatching();
            return null;
        }

        // Outside the lock, mount detection on a dropped SMB share can hang until the network timeout
        var choices = await ChooseWatchOrPollAsync();

        await ApplyLock.WaitAsync();
        try
        {
            folderPoller.Poll(choices.Where(c => c.Poll).Select(c => new PolledFolder(c.Folder, c.Mount)).ToList());
            return await nativeFileWatcher.WatchAsync(choices.Where(c => !c.Poll).Select(c => c.Folder).ToList());
        }
        finally
        {
            ApplyLock.Release();
        }
    }

    /// <summary>
    /// Detects each library folder's mount, records the mounts on the library, and picks watch or poll for the folder.
    /// A folder in two libraries polls if either polls
    /// </summary>
    private async Task<List<FolderChoice>> ChooseWatchOrPollAsync()
    {
        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.Folders))
            .Where(l => l.FolderWatchMode != FolderWatchMode.Off)
            .ToList();

        var choices = new Dictionary<string, FolderChoice>();
        foreach (var library in libraries)
        {
            var folders = library.Folders
                .Select(f => Parser.NormalizePath(f.Path))
                .Distinct()
                .Select(f => new FolderChoice(f, mountDetector.Detect(f), false))
                .ToList();

            var state = DetectedState(library.FolderWatchState, folders.Select(f => f.Mount));
            if (!state.Mounts.SequenceEqual(library.FolderWatchState.Mounts))
            {
                library.FolderWatchState = state;
                unitOfWork.LibraryRepository.Update(library);
            }

            var poll = state.Method(library.FolderWatchMode) is FolderWatchMethod.Polling or FolderWatchMethod.Failed;
            foreach (var folder in folders)
            {
                LogChoice(library, folder.Folder, folder.Mount, poll);
                if (choices.TryGetValue(folder.Folder, out var other) && (other.Poll || !poll)) continue;

                choices[folder.Folder] = folder with { Poll = poll };
            }
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        return choices.Values.ToList();
    }

    /// <summary>
    /// One entry per mount the library's folders are on now. A network mount is always Network; any other keeps what the
    /// audit learned about it, or starts in Trial
    /// </summary>
    internal static LibraryFolderWatchState DetectedState(LibraryFolderWatchState current, IEnumerable<MountInfo?> mounts)
    {
        var entries = mounts
            .OfType<MountInfo>()
            .DistinctBy(m => m.Fingerprint)
            .Select(m => new LibraryFolderWatchState.MountEntry
            {
                Fingerprint = m.Fingerprint,
                Result = KnownResult(current, m),
            })
            .ToList();

        return current with { Mounts = entries };
    }

    private static LibraryFolderWatchState.MountResult KnownResult(LibraryFolderWatchState current, MountInfo mount)
    {
        if (mount.IsNetwork) return LibraryFolderWatchState.MountResult.Network;

        var known = current.Mounts.FirstOrDefault(e => e.Fingerprint == mount.Fingerprint)?.Result;
        return known is null or LibraryFolderWatchState.MountResult.Network ? LibraryFolderWatchState.MountResult.Trial : known.Value;
    }

    private void LogChoice(Library library, string folder, MountInfo? mount, bool poll)
    {
        var choice = (library.FolderWatchMode, poll, mount?.IsNetwork) switch
        {
            (FolderWatchMode.Polling, _, _) => "polling folder times, as the library is set to Polling",
            (FolderWatchMode.FileWatcher, _, _) => "watching, as the library is set to File Watcher",
            (_, true, true) => "polling folder times instead of watching",
            (_, true, _) => $"polling folder times, as another folder of {library.Name} is on a network mount",
            _ => "watching",
        };
        var line = mount == null
            ? $"{folder}: could not read its mount, {choice}"
            : $"{folder}: mount {mount.Point} is {mount.Type} ({mount.Source}), {choice}";

        lock (LoggedChoices)
        {
            if (LoggedChoices.TryGetValue(folder, out var logged) && logged == line) return;
            LoggedChoices[folder] = line;
        }
        logger.LogInformation("[FolderWatchService] {Choice}", line);
    }

    public void StopWatching()
    {
        ApplyLock.Wait();
        try
        {
            nativeFileWatcher.Stop();
            folderPoller.Stop();
        }
        finally
        {
            ApplyLock.Release();
        }
    }

    public async Task RestartWatching()
    {
        logger.LogDebug("[FolderWatchService] Restarting watcher");
        await StartWatching();
    }

    /// <remarks>This is public only because Hangfire will invoke it. Do not call external to this class.</remarks>
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task TurnOffWatching()
    {
        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EnableFolderWatching);
        setting.Value = "false";
        unitOfWork.SettingsRepository.Update(setting);
        await unitOfWork.CommitAsync();

        StopWatching();

        logger.LogInformation("[FolderWatchService] Folder watching has been disabled");
    }

    internal static void EnqueueChange(string path, bool isDirectoryChange)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask<FolderWatchService>(w => w.ProcessChange(path, isDirectoryChange), checkRunningJobs: true)) return;

        BackgroundJob.Enqueue<FolderWatchService>(w => w.ProcessChange(path, isDirectoryChange));
    }

    /// <summary>
    /// Processes the file or folder change. If the change is a file change and not from a supported extension, it will be ignored.
    /// </summary>
    /// <remarks>This will ignore image files that are added to the system. However, they may still trigger scans due to folder changes.</remarks>
    /// <remarks>This is public only because Hangfire will invoke it. Do not call external to this class.</remarks>
    /// <param name="filePath">File or folder that changed</param>
    /// <param name="isDirectoryChange">If the change is on a directory and not a file</param>
    [DisableConcurrentExecution(60)]
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task ProcessChange(string filePath, bool isDirectoryChange = false)
    {
        var sw = Stopwatch.StartNew();
        logger.LogTrace("[FolderWatchService] Processing change of {FilePath}", filePath);
        try
        {
            // If the change occurs in a blacklisted folder path, then abort processing
            if (Parser.HasBlacklistedFolderInPath(filePath))
            {
                return;
            }

            // If not a directory change AND file is not an archive or book, ignore
            if (!isDirectoryChange &&
                !(Parser.IsArchive(filePath) || Parser.IsBook(filePath)))
            {
                logger.LogTrace("[FolderWatchService] Change from {FilePath} is not an archive or book, ignoring change", filePath);
                return;
            }

            await taskScheduler.EnqueueScanFolderFromWatcherAsync(filePath, isDirectoryChange);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FolderWatchService] An error occured when processing a watch event");
        }
        logger.LogTrace("[FolderWatchService] ProcessChange completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }
}
