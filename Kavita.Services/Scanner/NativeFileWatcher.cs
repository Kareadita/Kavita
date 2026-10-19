using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Responsible for watching the file system and processing change events. This is mainly responsible for invoking
/// Scanner to quickly pickup on changes.
/// </summary>
public class NativeFileWatcher : INativeFileWatcher
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NativeFileWatcher> _logger;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IMountDetectorService _mountDetector;
    private readonly IFolderPollerService _folderPoller;

    /// <summary>
    /// This is just here to prevent GC from Disposing our watchers
    /// </summary>
    private static readonly List<FileSystemWatcher> FileWatchers = [];

    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// Watcher events run on whichever scoped instance subscribed them, long after its scope is disposed
    /// </summary>
    private static readonly SemaphoreSlim WatcherLock = new(1, 1);
    /// <summary>
    /// Counts within a time frame how many times the buffer became full. Is used to reschedule NativeFileWatcher to start monitoring much later rather than instantly
    /// </summary>
    private static int _bufferFullCounter;
    private static int _restartCounter;
    private static DateTime _lastErrorTime = DateTime.MinValue;
    private static DateTime _retryScheduledUntil = DateTime.MinValue;
    private static TimeSpan _retryDelay = MinRetryDelay;

    internal static IReadOnlyList<FileSystemWatcher> Watchers => [.. FileWatchers];

    /// <summary>
    /// Each folder's last logged watch or poll line, so a restart after every library save does not repeat it
    /// </summary>
    private static readonly Dictionary<string, string> LoggedChoices = new();

    public NativeFileWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork,
        ILogger<NativeFileWatcher> logger, ITaskScheduler taskScheduler, IMountDetectorService mountDetector,
        IFolderPollerService folderPoller)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _taskScheduler = taskScheduler;
        _mountDetector = mountDetector;
        _folderPoller = folderPoller;
    }

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
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableFolderWatching)
        {
            _logger.LogInformation("Folder watching is disabled at the server level, thus ignoring any requests to create folder watching");
            StopWatching();
            return null;
        }

        // Outside the lock, mount detection and Exists on a dropped SMB share can hang until the network timeout
        var choices = await ChooseWatchOrPollAsync();
        var polled = choices.Where(c => c.Poll).ToList();
        var configuredFolders = choices.Where(c => !c.Poll).Select(c => c.Folder).ToList();
        var libraryFolders = configuredFolders.Where(_directoryService.Exists).ToList();

        await WatcherLock.WaitAsync();
        try
        {
            StopWatchers();
            _folderPoller.Poll(polled.Select(c => new PolledFolder(c.Folder, c.Mount)).ToList());

            _logger.LogInformation("[NativeFileWatcher] Starting file watchers for {Count} library folders", libraryFolders.Count);

            var watchedFolders = new List<string>();
            foreach (var libraryFolder in libraryFolders)
            {
                _logger.LogDebug("[NativeFileWatcher] Watching {FolderPath}", libraryFolder);
                if (TryStartWatcher(libraryFolder))
                {
                    watchedFolders.Add(libraryFolder);
                }
            }
            _logger.LogInformation("[NativeFileWatcher] Watching {Count} folders", watchedFolders.Count);

            var missingFolders = configuredFolders.Except(watchedFolders).ToList();
            if (missingFolders.Count == 0)
            {
                _retryDelay = MinRetryDelay;
                return null;
            }

            if (_retryScheduledUntil > DateTime.Now) return null;

            var retryIn = _retryDelay;
            _retryDelay = TimeSpan.FromTicks(Math.Min(retryIn.Ticks * 2, MaxRetryDelay.Ticks));
            _retryScheduledUntil = DateTime.Now + retryIn;
            _logger.LogWarning("[NativeFileWatcher] {Folders} cannot be reached or watched. Trying again in {Minutes} minutes",
                missingFolders, retryIn.TotalMinutes);
            return retryIn;
        }
        finally
        {
            WatcherLock.Release();
        }
    }

    /// <summary>
    /// Detects each library folder's mount, records the mounts on the library, and picks watch or poll for the folder.
    /// A folder in two libraries polls if either polls
    /// </summary>
    private async Task<List<FolderChoice>> ChooseWatchOrPollAsync()
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.Folders))
            .Where(l => l.FolderWatchMode != FolderWatchMode.Off)
            .ToList();

        var choices = new Dictionary<string, FolderChoice>();
        foreach (var library in libraries)
        {
            var folders = library.Folders
                .Select(f => Parser.NormalizePath(f.Path))
                .Distinct()
                .Select(f => new FolderChoice(f, _mountDetector.Detect(f), false))
                .ToList();

            var state = DetectedState(library.FolderWatchState, folders.Select(f => f.Mount));
            if (!state.Mounts.SequenceEqual(library.FolderWatchState.Mounts))
            {
                library.FolderWatchState = state;
                _unitOfWork.LibraryRepository.Update(library);
            }

            var poll = state.Method(library.FolderWatchMode) == FolderWatchMethod.Polling;
            foreach (var folder in folders)
            {
                LogChoice(library, folder.Folder, folder.Mount, poll);
                if (choices.TryGetValue(folder.Folder, out var other) && (other.Poll || !poll)) continue;

                choices[folder.Folder] = folder with { Poll = poll };
            }
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
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
        _logger.LogInformation("[NativeFileWatcher] {Choice}", line);
    }

    /// <summary>The folder can be gone by now even though Exists passed, the share dropped in between</summary>
    private bool TryStartWatcher(string libraryFolder)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(libraryFolder);

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            FileWatchers.Add(watcher);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NativeFileWatcher] Could not watch {FolderPath}", libraryFolder);
            watcher?.Dispose();
            return false;
        }
    }

    public void StopWatching()
    {
        WatcherLock.Wait();
        try
        {
            StopWatchers();
            _folderPoller.Stop();
        }
        finally
        {
            WatcherLock.Release();
        }
    }

    /// <remarks>Caller must hold <see cref="WatcherLock"/></remarks>
    private void StopWatchers()
    {
        if (FileWatchers.Count == 0) return;

        _logger.LogInformation("[NativeFileWatcher] Stopping watching folders");
        foreach (var fileSystemWatcher in FileWatchers)
        {
            fileSystemWatcher.Dispose();
        }
        FileWatchers.Clear();
    }

    public async Task RestartWatching()
    {
        _logger.LogDebug("[NativeFileWatcher] Restarting watcher");
        await StartWatching();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[NativeFileWatcher] Changed: {FullPath}, {Name}, {ChangeType}", e.FullPath, e.Name, e.ChangeType);
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        // NTFS holds a folder's Changed back until the folder is next listed, so Kavita's own validation would release it
        // and scan again. Every change inside the folder also raises an event on its file, or Renamed
        if (_directoryService.FileSystem.Directory.Exists(e.FullPath)) return;

        EnqueueChange(e.FullPath, false);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogTrace("[NativeFileWatcher] Renamed: {OldFullPath} to {FullPath}", e.OldFullPath, e.FullPath);
        if (_directoryService.FileSystem.Directory.Exists(e.FullPath))
        {
            EnqueueChange(e.FullPath, true);
            return;
        }

        if (!IsReadingFile(e.FullPath) && !IsReadingFile(e.OldFullPath)) return;

        EnqueueChange(Path.GetDirectoryName(e.FullPath)!, true);
    }

    private static bool IsReadingFile(string path) => Parser.IsArchive(path) || Parser.IsBook(path) || Parser.IsImage(path);

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[NativeFileWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        EnqueueChange(e.FullPath, _directoryService.FileSystem.Directory.Exists(e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[NativeFileWatcher] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        // The path is gone, so a file is told apart by a type Kavita reads: a folder "Vol. 1" has an extension too
        EnqueueChange(e.FullPath, !IsReadingFile(e.FullPath));
    }

    internal static void EnqueueChange(string path, bool isDirectoryChange)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask<NativeFileWatcher>(w => w.ProcessChange(path, isDirectoryChange), checkRunningJobs: true)) return;

        BackgroundJob.Enqueue<NativeFileWatcher>(w => w.ProcessChange(path, isDirectoryChange));
    }

    /// <summary>
    /// A folder that cannot be reached restarts watching right away, retrying until it is back. Any other error is counted: 3 in 10 minutes
    /// suspends watching for an hour, and after 3 suspends folder watching is turned off
    /// </summary>
    private void OnError(object sender, ErrorEventArgs e)
    {
        var watcher = (FileSystemWatcher) sender;
        var exception = e.GetException();

        // Windows can raise this synchronously inside EnableRaisingEvents, while StartWatchersAsync holds the lock
        Task.Run(async () =>
        {
            try
            {
                switch (await HandleErrorAsync(watcher, exception))
                {
                    case WatcherErrorAction.FolderUnreachable:
                        BackgroundJob.Enqueue(() => RestartWatching());
                        break;
                    case WatcherErrorAction.RestartNow:
                        BackgroundJob.Enqueue(() => RestartWatching());
                        BackgroundJob.Schedule(() => UpdateLastBufferOverflow(), TimeSpan.FromMinutes(10));
                        break;
                    case WatcherErrorAction.Suspend:
                        BackgroundJob.Schedule(() => RestartWatching(), TimeSpan.FromHours(1));
                        BackgroundJob.Schedule(() => UpdateLastBufferOverflow(), TimeSpan.FromMinutes(10));
                        break;
                    case WatcherErrorAction.TurnOff:
                        BackgroundJob.Enqueue(() => TurnOffWatching());
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NativeFileWatcher] Could not recover from a watcher error, folder watching is off until the next restart");
            }
        });
    }

    /// <summary>
    /// The first error from a live watcher stops every watcher. One network drop raises an error on every watcher at
    /// once, so the others find their watcher already stopped and are ignored
    /// </summary>
    internal async Task<WatcherErrorAction> HandleErrorAsync(FileSystemWatcher watcher, Exception exception)
    {
        string folder;
        await WatcherLock.WaitAsync();
        try
        {
            if (!FileWatchers.Contains(watcher))
            {
                _logger.LogDebug(exception, "[NativeFileWatcher] Ignoring an error from a watcher that was already stopped");
                return WatcherErrorAction.Ignore;
            }

            folder = watcher.Path;
            StopWatchers();
        }
        finally
        {
            WatcherLock.Release();
        }

        // Outside the lock, Exists on a dropped SMB share can hang until the network timeout
        var reachable = _directoryService.Exists(folder);

        await WatcherLock.WaitAsync();
        try
        {
            if (!reachable)
            {
                _logger.LogWarning(exception, "[NativeFileWatcher] {Folder} can no longer be reached, restarting watchers for the folders that still exist", folder);
                _retryScheduledUntil = DateTime.MinValue;
                _retryDelay = MinRetryDelay;
                return WatcherErrorAction.FolderUnreachable;
            }

            var now = DateTime.Now;
            _bufferFullCounter += 1;
            var previousErrorWithin10Minutes = (now - _lastErrorTime).TotalMinutes <= 10;
            _lastErrorTime = now;
            _logger.LogError(exception, "[NativeFileWatcher] An error occured, likely too many changes occured at once. Restarting Watchers {Current}/{Total}", _bufferFullCounter, 3);

            if (_restartCounter >= 3)
            {
                _logger.LogInformation("[NativeFileWatcher] Too many restarts occured, you either have limited inotify or an OS constraint. Kavita will turn off folder watching to prevent high utilization of resources");
                return WatcherErrorAction.TurnOff;
            }

            if (_bufferFullCounter >= 3 && previousErrorWithin10Minutes)
            {
                _logger.LogInformation("[NativeFileWatcher] Internal buffer has been overflown multiple times in past 10 minutes. Suspending file watching for an hour. Restart count: {RestartCount}", _restartCounter);
                _restartCounter++;
                return WatcherErrorAction.Suspend;
            }

            return WatcherErrorAction.RestartNow;
        }
        finally
        {
            WatcherLock.Release();
        }
    }

    /// <remarks>This is public only because Hangfire will invoke it. Do not call external to this class.</remarks>
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task TurnOffWatching()
    {
        var setting = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EnableFolderWatching);
        setting.Value = "false";
        _unitOfWork.SettingsRepository.Update(setting);
        await _unitOfWork.CommitAsync();

        StopWatching();

        _logger.LogInformation("[NativeFileWatcher] Folder watching has been disabled");
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
        _logger.LogTrace("[NativeFileWatcher] Processing change of {FilePath}", filePath);
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
                _logger.LogTrace("[NativeFileWatcher] Change from {FilePath} is not an archive or book, ignoring change", filePath);
                return;
            }

            await _taskScheduler.EnqueueScanFolderFromWatcherAsync(filePath, isDirectoryChange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NativeFileWatcher] An error occured when processing a watch event");
        }
        _logger.LogTrace("[NativeFileWatcher] ProcessChange completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// This is called via Hangfire to decrement the counter. Must work around a lock
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public static void UpdateLastBufferOverflow()
    {
        WatcherLock.Wait();
        try
        {
            if (_bufferFullCounter == 0) return;
            _bufferFullCounter -= 1;
        }
        finally
        {
            WatcherLock.Release();
        }
    }

    internal static void ResetErrorCounters()
    {
        _bufferFullCounter = 0;
        _restartCounter = 0;
        _lastErrorTime = DateTime.MinValue;
        _retryScheduledUntil = DateTime.MinValue;
        _retryDelay = MinRetryDelay;
    }
}

internal enum WatcherErrorAction
{
    Ignore,
    FolderUnreachable,
    RestartNow,
    Suspend,
    TurnOff,
}
