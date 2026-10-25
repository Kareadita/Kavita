using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Services.Scanner;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Watches library folders with <see cref="FileSystemWatcher"/> and hands each change to <see cref="FolderWatchService"/>.
/// Records when each folder last raised an event, for the scan audit
/// </summary>
public class NativeFileWatcherService(IFileSystem fileSystem, ILogger<NativeFileWatcherService> logger, TimeProvider timeProvider)
    : INativeFileWatcherService
{
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan ErrorWindow = TimeSpan.FromMinutes(10);
    /// <summary>
    /// Each restart is immediate, so one large copy overflows the new watcher again while its events still arrive.
    /// Errors closer together than this count once
    /// </summary>
    private static readonly TimeSpan OneBurst = TimeSpan.FromMinutes(1);
    private const int ErrorsToDowngrade = 3;

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTime _retryScheduledUntil = DateTime.MinValue;
    private TimeSpan _retryDelay = MinRetryDelay;

    /// <summary>
    /// Each watched folder's errors within <see cref="ErrorWindow"/>. Caller must hold <see cref="_lock"/>
    /// </summary>
    private readonly Dictionary<string, List<DateTime>> _errorsUtc = new();
    private readonly ConcurrentDictionary<string, DateTime> _startedUtc = new();
    private readonly ConcurrentDictionary<string, DateTime> _prunedBeforeUtc = new();
    /// <summary>
    /// Folder to its last event. An event records the folder it happened in and that folder's parent
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastEventUtc = new();
    /// <summary>
    /// Folders a Created event made, which cover everything below them
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _createdUtc = new();

    internal IReadOnlyList<FileSystemWatcher> Watchers => [.. _watchers];

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<TimeSpan?> WatchAsync(IReadOnlyList<string> folders)
    {
        // Outside the lock, Exists on a dropped SMB share can hang until the network timeout
        var reachable = folders.Where(fileSystem.Directory.Exists).ToList();

        await _lock.WaitAsync();
        try
        {
            StopWatchers();

            logger.LogDebug("[NativeFileWatcherService] Starting file watchers for {Count} library folders", reachable.Count);

            var watchedFolders = new List<string>();
            foreach (var libraryFolder in reachable)
            {
                logger.LogDebug("[NativeFileWatcherService] Watching {FolderPath}", libraryFolder);
                if (TryStartWatcher(libraryFolder))
                {
                    watchedFolders.Add(libraryFolder);
                    _startedUtc[Parser.NormalizePath(libraryFolder)] = UtcNow;
                }
            }
            logger.LogDebug("[NativeFileWatcherService] Watching {Count} folders", watchedFolders.Count);

            var missingFolders = folders.Except(watchedFolders).ToList();
            if (missingFolders.Count == 0)
            {
                _retryDelay = MinRetryDelay;
                return null;
            }

            if (_retryScheduledUntil > DateTime.Now) return null;

            var retryIn = _retryDelay;
            _retryDelay = TimeSpan.FromTicks(Math.Min(retryIn.Ticks * 2, MaxRetryDelay.Ticks));
            _retryScheduledUntil = DateTime.Now + retryIn;
            logger.LogWarning("[NativeFileWatcherService] {Folders} cannot be reached or watched. Trying again in {Minutes} minutes",
                missingFolders, retryIn.TotalMinutes);
            return retryIn;
        }
        finally
        {
            _lock.Release();
        }
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
            _watchers.Add(watcher);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[NativeFileWatcherService] Could not watch {FolderPath}", libraryFolder);
            watcher?.Dispose();
            return false;
        }
    }

    public void Stop()
    {
        _lock.Wait();
        try
        {
            StopWatchers();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <remarks>Caller must hold <see cref="_lock"/></remarks>
    private void StopWatchers()
    {
        _startedUtc.Clear();
        if (_watchers.Count == 0) return;

        logger.LogDebug("[NativeFileWatcherService] Stopping watching folders");
        foreach (var fileSystemWatcher in _watchers)
        {
            fileSystemWatcher.Dispose();
        }
        _watchers.Clear();
    }

    public bool SawChange(string folder, DateTime sinceUtc)
    {
        if (_lastEventUtc.TryGetValue(folder, out var lastEvent) && lastEvent >= sinceUtc) return true;

        return _createdUtc.Any(created => created.Value >= sinceUtc && folder.IsSameOrInsideFolder(created.Key));
    }

    public DateTime? WatchingSince(string folder)
    {
        var watchedFolder = folder.DeepestContainingFolder(_startedUtc.Keys);
        if (watchedFolder == null || !_startedUtc.TryGetValue(watchedFolder, out var started)) return null;

        // Library folders can nest, and pruning the outer one also drops events inside the inner one
        var pruned = _prunedBeforeUtc
            .Where(p => folder.IsSameOrInsideFolder(p.Key))
            .Select(p => p.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        return pruned > started ? pruned : started;
    }

    public void Prune(string libraryFolder, DateTime beforeUtc)
    {
        var root = Parser.NormalizePath(libraryFolder);
        _prunedBeforeUtc.AddOrUpdate(root, beforeUtc, (_, current) => current > beforeUtc ? current : beforeUtc);

        foreach (var events in new[] { _lastEventUtc, _createdUtc })
        {
            foreach (var old in events.Where(e => e.Value < beforeUtc && e.Key.IsSameOrInsideFolder(root)).ToList())
            {
                events.TryRemove(old);
            }
        }
    }

    private void RecordEvent(string path, bool isFolder, bool created = false)
    {
        var now = UtcNow;
        var normalized = Parser.NormalizePath(path);
        var folder = isFolder ? normalized : normalized.FolderOf();

        _lastEventUtc[folder] = now;
        _lastEventUtc[folder.FolderOf()] = now;
        if (created)
        {
            _createdUtc[folder] = now;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Changed: {FullPath}, {Name}, {ChangeType}", e.FullPath, e.Name, e.ChangeType);
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        // NTFS holds a folder's Changed back until the folder is next listed, so Kavita's own validation would release it
        // and scan again, and the scan audit would count the scan's own listing as the watcher seeing the change. Every
        // change inside the folder also raises an event on its file, or Renamed
        if (fileSystem.Directory.Exists(e.FullPath)) return;

        RecordEvent(e.FullPath, false);
        FolderWatchService.EnqueueChange(e.FullPath, false);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Renamed: {OldFullPath} to {FullPath}", e.OldFullPath, e.FullPath);
        var isFolder = fileSystem.Directory.Exists(e.FullPath);
        RecordEvent(e.FullPath, isFolder);
        RecordEvent(e.OldFullPath, isFolder);
        if (isFolder)
        {
            FolderWatchService.EnqueueChange(e.FullPath, true);
            return;
        }

        if (!IsReadingFile(e.FullPath) && !IsReadingFile(e.OldFullPath)) return;

        FolderWatchService.EnqueueChange(Path.GetDirectoryName(e.FullPath)!, true);
    }

    private static bool IsReadingFile(string path) => Parser.IsArchive(path) || Parser.IsBook(path) || Parser.IsImage(path);

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        var isFolder = fileSystem.Directory.Exists(e.FullPath);
        RecordEvent(e.FullPath, isFolder, isFolder);
        FolderWatchService.EnqueueChange(e.FullPath, isFolder);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        // The path is gone, so a file is told apart by a type Kavita reads: a folder "Vol. 1" has an extension too
        var isFolder = !IsReadingFile(e.FullPath);
        RecordEvent(e.FullPath, isFolder);
        FolderWatchService.EnqueueChange(e.FullPath, isFolder);
    }

    /// <summary>
    /// A folder that cannot be reached restarts watching right away, retrying until it is back. Any other error restarts
    /// watching too, and the third on one folder within 10 minutes makes the libraries with that folder poll
    /// </summary>
    private void OnError(object sender, ErrorEventArgs e)
    {
        var watcher = (FileSystemWatcher) sender;
        var exception = e.GetException();
        var folder = Parser.NormalizePath(watcher.Path);

        // Windows can raise this synchronously inside EnableRaisingEvents, while WatchAsync holds the lock
        Task.Run(async () =>
        {
            try
            {
                ScheduleRecovery(await HandleErrorAsync(watcher, exception), folder);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[NativeFileWatcherService] Could not recover from a watcher error, folder watching is off until the next restart");
            }
        });
    }

    private static void ScheduleRecovery(WatcherErrorAction action, string folder)
    {
        switch (action)
        {
            case WatcherErrorAction.FolderUnreachable:
            case WatcherErrorAction.RestartNow:
                BackgroundJob.Enqueue<IFolderWatchService>(s => s.RestartWatching());
                break;
            case WatcherErrorAction.Downgrade:
                BackgroundJob.Enqueue<IFolderWatchService>(s => s.DowngradeFolder(folder, LibraryFolderWatchState.DowngradeReason.Errors));
                break;
        }
    }

    /// <summary>
    /// The first error from a live watcher stops every watcher. One network drop raises an error on every watcher at
    /// once, so the others find their watcher already stopped and are ignored
    /// </summary>
    internal async Task<WatcherErrorAction> HandleErrorAsync(FileSystemWatcher watcher, Exception exception)
    {
        string folder;
        await _lock.WaitAsync();
        try
        {
            if (!_watchers.Contains(watcher))
            {
                logger.LogDebug(exception, "[NativeFileWatcherService] Ignoring an error from a watcher that was already stopped");
                return WatcherErrorAction.Ignore;
            }

            folder = Parser.NormalizePath(watcher.Path);
            StopWatchers();
        }
        finally
        {
            _lock.Release();
        }

        // Outside the lock, Exists on a dropped SMB share can hang until the network timeout
        var reachable = fileSystem.Directory.Exists(folder);

        await _lock.WaitAsync();
        try
        {
            if (!reachable)
            {
                logger.LogWarning(exception, "[NativeFileWatcherService] {Folder} can no longer be reached, restarting watchers for the folders that still exist", folder);
                _retryScheduledUntil = DateTime.MinValue;
                _retryDelay = MinRetryDelay;
                return WatcherErrorAction.FolderUnreachable;
            }

            var now = UtcNow;
            var errors = _errorsUtc.TryGetValue(folder, out var known) ? known : _errorsUtc[folder] = [];
            errors.RemoveAll(at => now - at > ErrorWindow);
            if (errors.Count > 0 && now - errors[^1] < OneBurst)
            {
                logger.LogDebug(exception, "[NativeFileWatcherService] The watcher on {Folder} failed again in the same burst of changes, restarting watchers without counting it",
                    folder);
                return WatcherErrorAction.RestartNow;
            }
            errors.Add(now);

            if (errors.Count >= ErrorsToDowngrade)
            {
                _errorsUtc.Remove(folder);
                logger.LogWarning(exception, "[NativeFileWatcherService] The watcher on {Folder} failed {Count} times in {Minutes} minutes, likely too many changes at once. Libraries with this folder will poll instead",
                    folder, ErrorsToDowngrade, ErrorWindow.TotalMinutes);
                return WatcherErrorAction.Downgrade;
            }

            logger.LogWarning(exception, "[NativeFileWatcherService] The watcher on {Folder} failed, likely too many changes at once. Restarting watchers {Current}/{Total}",
                folder, errors.Count, ErrorsToDowngrade);
            return WatcherErrorAction.RestartNow;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal enum WatcherErrorAction
    {
        Ignore,
        FolderUnreachable,
        RestartNow,
        Downgrade,
    }
}
