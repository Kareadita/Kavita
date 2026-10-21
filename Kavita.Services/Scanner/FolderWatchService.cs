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
using Kavita.Common.Extensions;
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
    IMountDetectorService mountDetector, INativeFileWatcherService nativeFileWatcher, IFolderPollerService folderPoller,
    IDirectoryService directoryService)
    : IFolderWatchService
{
    /// <summary>
    /// Covers the poll and the watch apply together, so two restarts at once never leave a folder both polled and watched
    /// </summary>
    private static readonly SemaphoreSlim ApplyLock = new(1, 1);

    private static readonly TimeSpan AuditClockSkew = TimeSpan.FromMinutes(2);
    private const int SuccessesToLeaveTrial = 3;

    /// <summary>
    /// Each folder's last logged watch or poll line, so a restart after every library save does not repeat it
    /// </summary>
    private static readonly Dictionary<string, string> LoggedChoices = new();

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
            if (!IsSameState(state, library.FolderWatchState))
            {
                if (library.FolderWatchState.Downgrade is { } dropped && state.Downgrade == null)
                {
                    logger.LogInformation("[FolderWatchService] {Folder} ({Library} library) is now on a different disk/share, clearing watcher errors and rewatching",
                        dropped.Folder, library.Name);
                }

                library.FolderWatchState = state;
                unitOfWork.LibraryRepository.Update(library);
            }

            var needsPolling = state.WatchMethod(library.FolderWatchMode) is FolderWatchMethod.Polling or FolderWatchMethod.Failed;
            foreach (var folder in folders)
            {
                LogChoice(library, folder.Folder, folder.Mount);
                if (choices.TryGetValue(folder.Folder, out var other) && (other.Poll || !needsPolling)) continue;

                choices[folder.Folder] = folder with { Poll = needsPolling };
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
    /// audit learned about it, or starts in Trial. A downgrade is kept only while its folder's mount is still one of them
    /// </summary>
    internal static LibraryFolderWatchState DetectedState(LibraryFolderWatchState current, IEnumerable<MountInfo?> mounts)
    {
        var entries = mounts
            .OfType<MountInfo>()
            .DistinctBy(m => m.Fingerprint)
            .Select(m => GetKnownEntry(current, m))
            .ToList();

        // A folder whose mount could not be read has no fingerprint to compare, so its downgrade waits for Retry
        var downgrade = current.Downgrade is { } marker && (marker.Fingerprint == null || entries.Exists(e => e.Fingerprint == marker.Fingerprint))
            ? marker
            : null;

        return current with { Mounts = entries, Downgrade = downgrade };
    }

    /// <summary>
    /// The stored entry for this mount, with its result and counters, or a fresh one: Network for a network mount, Trial otherwise
    /// </summary>
    private static LibraryFolderWatchState.MountEntry GetKnownEntry(LibraryFolderWatchState current, MountInfo mount)
    {
        var entry = new LibraryFolderWatchState.MountEntry { Fingerprint = mount.Fingerprint, Result = LibraryFolderWatchState.MountResult.Network };
        if (mount.IsNetwork) return entry;

        var known = current.Mounts.FirstOrDefault(e => e.Fingerprint == mount.Fingerprint);
        return known is null or { Result: LibraryFolderWatchState.MountResult.Network }
            ? entry with { Result = LibraryFolderWatchState.MountResult.Trial }
            : known;
    }

    private static bool IsSameState(LibraryFolderWatchState a, LibraryFolderWatchState b)
    {
        return a.Mounts.SequenceEqual(b.Mounts) && a.Downgrade == b.Downgrade;
    }

    private void LogChoice(Library library, string folder, MountInfo? mount)
    {
        var choice = DescribeChoice(library, mount);
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

    /// <summary>
    /// Why the folder is watched or polled, in the order the reasons win
    /// </summary>
    private static string DescribeChoice(Library library, MountInfo? mount)
    {
        if (library.FolderWatchMode == FolderWatchMode.Polling) return "polling folder times, as the library is set to Polling";

        var state = library.FolderWatchState;
        var method = state.WatchMethod(library.FolderWatchMode);
        if (method == FolderWatchMethod.Failed)
        {
            return state.Downgrade != null
                ? $"polling folder times until Retry, as the watcher kept failing on {state.Downgrade.Folder}"
                : "polling folder times until Retry, as the watcher missed a change";
        }

        if (library.FolderWatchMode == FolderWatchMode.FileWatcher) return "watching, as the library is set to File Watcher";
        if (mount is { IsNetwork: true }) return "polling folder times instead of watching";
        if (method == FolderWatchMethod.Polling) return $"polling folder times, as another folder of {library.Name} is on a network mount";

        return "watching";
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

    /// <summary>
    /// A file watcher can fail silently: some mounts never report changes made from another machine. Every scan that
    /// found a folder changed is the proof of what the watcher should have seen, so the watcher earns Watching by
    /// seeing those changes, and one it missed sends the library to polling
    /// </summary>
    public async Task AuditScanAsync(int libraryId, IReadOnlyCollection<string> readFolders, DateTime scanStartedUtc, bool isLibraryScan)
    {
        if (!(await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableFolderWatching) return;

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        if (library == null) return;

        var roots = library.Folders.Select(f => Parser.NormalizePath(f.Path)).Distinct().ToList();
        if (isLibraryScan)
        {
            // That scan read every folder that changed before it started, so their events are no longer needed
            foreach (var root in roots)
            {
                nativeFileWatcher.Prune(root, scanStartedUtc - TaskScheduler.WatcherScanCap);
            }
        }

        var state = library.FolderWatchState;
        if (state.WatchMethod(library.FolderWatchMode) is not (FolderWatchMethod.Trial or FolderWatchMethod.FileWatcher)) return;

        var fingerprints = roots.ToDictionary(r => r, r => mountDetector.Detect(r)?.Fingerprint);
        var audits = readFolders
            .Select(folder => new FolderAudit(folder, folder.DeepestContainingFolder(roots) is { } root ? fingerprints[root] : null))
            .Where(audit => audit.Fingerprint != null)
            .Select(audit => audit with { Outcome = Judge(audit.Folder, scanStartedUtc) })
            .ToList();
        foreach (var audit in audits)
        {
            logger.LogDebug("[FolderWatchService] {Library}: audit of {Folder} is {Outcome}", library.Name, audit.Folder, audit.Outcome);
        }

        var audited = state with
        {
            Mounts = state.Mounts.Select(entry => AuditedEntry(library, entry, audits, isLibraryScan)).ToList(),
        };
        if (IsSameState(audited, state)) return;

        library.FolderWatchState = audited;
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        if (library.FolderWatchMode == FolderWatchMode.Automatic && audited.WatchMethod(library.FolderWatchMode) == FolderWatchMethod.Failed)
        {
            await RestartWatching();
        }
    }

    /// <summary>
    /// Success when the watcher saw the folder near its last change. With no event, a change the watcher could not have
    /// seen, or whose own scan may still be pending, is skipped. Success is checked first: a watcher's own scan always
    /// falls in the pending window
    /// </summary>
    private AuditOutcome Judge(string folder, DateTime scanStartedUtc)
    {
        if (!directoryService.FileSystem.Directory.Exists(folder)) return AuditOutcome.Skipped;

        var changedUtc = directoryService.FileSystem.Directory.GetLastWriteTimeUtc(folder);
        // 2-min covers clock skew between Kavita and a Windows share
        if (nativeFileWatcher.SawChange(folder, changedUtc - AuditClockSkew)) return AuditOutcome.Success;

        var watchingSince = nativeFileWatcher.WatchingSince(folder);
        if (watchingSince == null || changedUtc < watchingSince + AuditClockSkew) return AuditOutcome.Skipped;
        if (changedUtc > scanStartedUtc - TaskScheduler.WatcherScanCap) return AuditOutcome.Skipped;

        return AuditOutcome.Miss;
    }

    private LibraryFolderWatchState.MountEntry AuditedEntry(Library library, LibraryFolderWatchState.MountEntry entry,
        List<FolderAudit> audits, bool isLibraryScan)
    {
        if (entry.Result is not (LibraryFolderWatchState.MountResult.Trial or LibraryFolderWatchState.MountResult.Watching)) return entry;

        var mine = audits.Where(a => a.Fingerprint == entry.Fingerprint).ToList();
        var miss = mine.FirstOrDefault(a => a.Outcome == AuditOutcome.Miss);
        if (miss != null)
        {
            if (library.FolderWatchMode == FolderWatchMode.FileWatcher)
            {
                logger.LogWarning("[FolderWatchService] {Library}: the watcher missed a change in {Folder}, still watching as the library is set to File Watcher",
                    library.Name, miss.Folder);
            }
            else
            {
                logger.LogWarning("[FolderWatchService] {Library} switched from {Result} -> Polling until Retry, the watcher missed a change in {Folder}",
                    library.Name, entry.Result, miss.Folder);
            }
            return entry with { Result = LibraryFolderWatchState.MountResult.Failed };
        }

        var successes = entry.Successes + (mine.Exists(a => a.Outcome == AuditOutcome.Success) ? 1 : 0);
        var auditedScans = entry.AuditedScans + (isLibraryScan ? 1 : 0);
        var result = entry.Result == LibraryFolderWatchState.MountResult.Trial && successes >= SuccessesToLeaveTrial && auditedScans >= 1
            ? LibraryFolderWatchState.MountResult.Watching
            : entry.Result;

        if (result != entry.Result)
        {
            logger.LogInformation("[FolderWatchService] {Library} switched from Trial -> Watching, the watcher saw the changes of {Successes} scans and missed none in a library scan",
                library.Name, successes);
        }

        return entry with { Result = result, Successes = successes, AuditedScans = auditedScans };
    }

    public async Task RetryAsync(int libraryId)
    {
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        if (library == null) return;

        var state = library.FolderWatchState;
        var retried = state with
        {
            Mounts = state.Mounts
                .Select(m => m.Result == LibraryFolderWatchState.MountResult.Failed
                    ? new LibraryFolderWatchState.MountEntry { Fingerprint = m.Fingerprint, Result = LibraryFolderWatchState.MountResult.Trial }
                    : m)
                .ToList(),
            Downgrade = null,
        };
        if (IsSameState(retried, state)) return;

        library.FolderWatchState = retried;
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();
        logger.LogInformation("[FolderWatchService] {Library}: Retry cleared the watcher failures, watching again", library.Name);

        await RestartWatching();
    }

    public async Task DowngradeFolder(string folder, LibraryFolderWatchState.DowngradeReason reason)
    {
        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.Folders))
            .Where(l => l.FolderWatchMode is FolderWatchMode.Automatic or FolderWatchMode.FileWatcher
                        && l.Folders.Any(f => Parser.NormalizePath(f.Path) == folder))
            .ToList();

        var marker = new LibraryFolderWatchState.DowngradeMarker
        {
            Reason = reason,
            Folder = folder,
            Fingerprint = mountDetector.Detect(folder)?.Fingerprint,
            AtUtc = DateTime.UtcNow,
        };
        foreach (var library in libraries)
        {
            var from = library.FolderWatchState.WatchMethod(library.FolderWatchMode);
            library.FolderWatchState = library.FolderWatchState with { Downgrade = marker };
            unitOfWork.LibraryRepository.Update(library);
            logger.LogWarning("[FolderWatchService] {Library} switched from {From} -> Polling until Retry due to watcher failing on {Folder}",
                library.Name, from, folder);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        await RestartWatching();
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

    private sealed record FolderChoice(string Folder, MountInfo? Mount, bool Poll);

    private sealed record FolderAudit(string Folder, string? Fingerprint, AuditOutcome Outcome = AuditOutcome.Skipped);

    private enum AuditOutcome
    {
        Skipped,
        Success,
        Miss,
    }
}
