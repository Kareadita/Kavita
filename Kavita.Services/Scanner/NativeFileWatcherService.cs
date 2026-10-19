using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Watches library folders with <see cref="FileSystemWatcher"/> and hands each change to <see cref="FolderWatchService"/>
/// </summary>
public class NativeFileWatcherService(IFileSystem fileSystem, ILogger<NativeFileWatcherService> logger) : INativeFileWatcherService
{
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    /// <summary>
    /// Counts within a time frame how many times the buffer became full. Is used to reschedule watching to start much later rather than instantly
    /// </summary>
    private int _bufferFullCounter;
    private int _restartCounter;
    private DateTime _lastErrorTime = DateTime.MinValue;
    private DateTime _retryScheduledUntil = DateTime.MinValue;
    private TimeSpan _retryDelay = MinRetryDelay;

    internal IReadOnlyList<FileSystemWatcher> Watchers => [.. _watchers];

    public async Task<TimeSpan?> WatchAsync(IReadOnlyList<string> folders)
    {
        // Outside the lock, Exists on a dropped SMB share can hang until the network timeout
        var reachable = folders.Where(fileSystem.Directory.Exists).ToList();

        await _lock.WaitAsync();
        try
        {
            StopWatchers();

            logger.LogInformation("[NativeFileWatcherService] Starting file watchers for {Count} library folders", reachable.Count);

            var watchedFolders = new List<string>();
            foreach (var libraryFolder in reachable)
            {
                logger.LogDebug("[NativeFileWatcherService] Watching {FolderPath}", libraryFolder);
                if (TryStartWatcher(libraryFolder))
                {
                    watchedFolders.Add(libraryFolder);
                }
            }
            logger.LogInformation("[NativeFileWatcherService] Watching {Count} folders", watchedFolders.Count);

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
        if (_watchers.Count == 0) return;

        logger.LogInformation("[NativeFileWatcherService] Stopping watching folders");
        foreach (var fileSystemWatcher in _watchers)
        {
            fileSystemWatcher.Dispose();
        }
        _watchers.Clear();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Changed: {FullPath}, {Name}, {ChangeType}", e.FullPath, e.Name, e.ChangeType);
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        // NTFS holds a folder's Changed back until the folder is next listed, so Kavita's own validation would release it
        // and scan again. Every change inside the folder also raises an event on its file, or Renamed
        if (fileSystem.Directory.Exists(e.FullPath)) return;

        FolderWatchService.EnqueueChange(e.FullPath, false);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Renamed: {OldFullPath} to {FullPath}", e.OldFullPath, e.FullPath);
        if (fileSystem.Directory.Exists(e.FullPath))
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
        FolderWatchService.EnqueueChange(e.FullPath, fileSystem.Directory.Exists(e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        logger.LogTrace("[NativeFileWatcherService] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        // The path is gone, so a file is told apart by a type Kavita reads: a folder "Vol. 1" has an extension too
        FolderWatchService.EnqueueChange(e.FullPath, !IsReadingFile(e.FullPath));
    }

    /// <summary>
    /// A folder that cannot be reached restarts watching right away, retrying until it is back. Any other error is counted: 3 in 10 minutes
    /// suspends watching for an hour, and after 3 suspends folder watching is turned off
    /// </summary>
    private void OnError(object sender, ErrorEventArgs e)
    {
        var watcher = (FileSystemWatcher) sender;
        var exception = e.GetException();

        // Windows can raise this synchronously inside EnableRaisingEvents, while WatchAsync holds the lock
        Task.Run(async () =>
        {
            try
            {
                ScheduleRecovery(await HandleErrorAsync(watcher, exception));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[NativeFileWatcherService] Could not recover from a watcher error, folder watching is off until the next restart");
            }
        });
    }

    internal static void ScheduleRecovery(WatcherErrorAction action)
    {
        switch (action)
        {
            case WatcherErrorAction.FolderUnreachable:
                BackgroundJob.Enqueue<IFolderWatchService>(s => s.RestartWatching());
                break;
            case WatcherErrorAction.RestartNow:
                BackgroundJob.Enqueue<IFolderWatchService>(s => s.RestartWatching());
                ScheduleBufferOverflowDecrement();
                break;
            case WatcherErrorAction.Suspend:
                BackgroundJob.Schedule<IFolderWatchService>(s => s.RestartWatching(), TimeSpan.FromHours(1));
                ScheduleBufferOverflowDecrement();
                break;
            case WatcherErrorAction.TurnOff:
                BackgroundJob.Enqueue<FolderWatchService>(s => s.TurnOffWatching());
                break;
        }
    }

    /// <remarks>
    /// Must stay typed on the interface. Hangfire stores a captured object's runtime class and builds a new instance of it,
    /// so the <c>() => UpdateLastBufferOverflow()</c> form would decrement a throwaway counter, not this singleton's
    /// </remarks>
    private static void ScheduleBufferOverflowDecrement()
    {
        BackgroundJob.Schedule<INativeFileWatcherService>(s => s.UpdateLastBufferOverflow(), TimeSpan.FromMinutes(10));
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

            folder = watcher.Path;
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

            var now = DateTime.Now;
            _bufferFullCounter += 1;
            var previousErrorWithin10Minutes = (now - _lastErrorTime).TotalMinutes <= 10;
            _lastErrorTime = now;
            logger.LogError(exception, "[NativeFileWatcherService] An error occured, likely too many changes occured at once. Restarting Watchers {Current}/{Total}", _bufferFullCounter, 3);

            if (_restartCounter >= 3)
            {
                logger.LogInformation("[NativeFileWatcherService] Too many restarts occured, you either have limited inotify or an OS constraint. Kavita will turn off folder watching to prevent high utilization of resources");
                return WatcherErrorAction.TurnOff;
            }

            if (_bufferFullCounter >= 3 && previousErrorWithin10Minutes)
            {
                logger.LogInformation("[NativeFileWatcherService] Internal buffer has been overflown multiple times in past 10 minutes. Suspending file watching for an hour. Restart count: {RestartCount}", _restartCounter);
                _restartCounter++;
                return WatcherErrorAction.Suspend;
            }

            return WatcherErrorAction.RestartNow;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void UpdateLastBufferOverflow()
    {
        _lock.Wait();
        try
        {
            if (_bufferFullCounter == 0) return;
            _bufferFullCounter -= 1;
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
        Suspend,
        TurnOff,
    }
}
