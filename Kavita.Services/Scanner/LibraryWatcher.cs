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
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Responsible for watching the file system and processing change events. This is mainly responsible for invoking
/// Scanner to quickly pickup on changes.
/// </summary>
public class LibraryWatcher : ILibraryWatcher
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LibraryWatcher> _logger;
    private readonly ITaskScheduler _taskScheduler;

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
    /// Counts within a time frame how many times the buffer became full. Is used to reschedule LibraryWatcher to start monitoring much later rather than instantly
    /// </summary>
    private static int _bufferFullCounter;
    private static int _restartCounter;
    private static DateTime _lastErrorTime = DateTime.MinValue;
    private static DateTime _retryScheduledUntil = DateTime.MinValue;
    private static TimeSpan _retryDelay = MinRetryDelay;

    internal static IReadOnlyList<FileSystemWatcher> Watchers => [.. FileWatchers];

    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork,
        ILogger<LibraryWatcher> logger, ITaskScheduler taskScheduler)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _taskScheduler = taskScheduler;
    }

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

        var configuredFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .Where(l => l.FolderWatching)
            .SelectMany(l => l.Folders)
            .Select(Parser.NormalizePath)
            .Distinct()
            .ToList();
        // Outside the lock, Exists on a dropped SMB share can hang until the network timeout
        var libraryFolders = configuredFolders.Where(_directoryService.Exists).ToList();

        await WatcherLock.WaitAsync();
        try
        {
            StopWatchers();

            _logger.LogInformation("[LibraryWatcher] Starting file watchers for {Count} library folders", libraryFolders.Count);

            var watchedFolders = new List<string>();
            foreach (var libraryFolder in libraryFolders)
            {
                _logger.LogDebug("[LibraryWatcher] Watching {FolderPath}", libraryFolder);
                if (TryStartWatcher(libraryFolder))
                {
                    watchedFolders.Add(libraryFolder);
                }
            }
            _logger.LogInformation("[LibraryWatcher] Watching {Count} folders", watchedFolders.Count);

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
            _logger.LogWarning("[LibraryWatcher] {Folders} cannot be reached or watched. Trying again in {Minutes} minutes",
                missingFolders, retryIn.TotalMinutes);
            return retryIn;
        }
        finally
        {
            WatcherLock.Release();
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
            watcher.Error += OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            FileWatchers.Add(watcher);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LibraryWatcher] Could not watch {FolderPath}", libraryFolder);
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

        _logger.LogInformation("[LibraryWatcher] Stopping watching folders");
        foreach (var fileSystemWatcher in FileWatchers)
        {
            fileSystemWatcher.Dispose();
        }
        FileWatchers.Clear();
    }

    public async Task RestartWatching()
    {
        _logger.LogDebug("[LibraryWatcher] Restarting watcher");
        await StartWatching();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[LibraryWatcher] Changed: {FullPath}, {Name}, {ChangeType}", e.FullPath, e.Name, e.ChangeType);
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        EnqueueChange(e.FullPath, _directoryService.FileSystem.Directory.Exists(e.FullPath));
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[LibraryWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        EnqueueChange(e.FullPath, _directoryService.FileSystem.Directory.Exists(e.FullPath));
    }

    /// <remarks>
    /// NTFS also raises Changed on the parent folder of a deleted file, inotify does not, so file deletes must pass through
    /// </remarks>
    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[LibraryWatcher] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        // The path is gone, so a file is told apart by a type Kavita reads: a folder "Vol. 1" has an extension too
        var isFile = Parser.IsArchive(e.FullPath) || Parser.IsBook(e.FullPath) || Parser.IsImage(e.FullPath);
        EnqueueChange(e.FullPath, !isFile);
    }

    private void EnqueueChange(string path, bool isDirectoryChange)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask("LibraryWatcher", "ProcessChange", [path, isDirectoryChange],
                checkRunningJobs: true))
        {
            return;
        }

        BackgroundJob.Enqueue(() => ProcessChange(path, isDirectoryChange));
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
                _logger.LogError(ex, "[LibraryWatcher] Could not recover from a watcher error, folder watching is off until the next restart");
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
                _logger.LogDebug(exception, "[LibraryWatcher] Ignoring an error from a watcher that was already stopped");
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
                _logger.LogWarning(exception, "[LibraryWatcher] {Folder} can no longer be reached, restarting watchers for the folders that still exist", folder);
                _retryScheduledUntil = DateTime.MinValue;
                _retryDelay = MinRetryDelay;
                return WatcherErrorAction.FolderUnreachable;
            }

            var now = DateTime.Now;
            _bufferFullCounter += 1;
            var previousErrorWithin10Minutes = (now - _lastErrorTime).TotalMinutes <= 10;
            _lastErrorTime = now;
            _logger.LogError(exception, "[LibraryWatcher] An error occured, likely too many changes occured at once. Restarting Watchers {Current}/{Total}", _bufferFullCounter, 3);

            if (_restartCounter >= 3)
            {
                _logger.LogInformation("[LibraryWatcher] Too many restarts occured, you either have limited inotify or an OS constraint. Kavita will turn off folder watching to prevent high utilization of resources");
                return WatcherErrorAction.TurnOff;
            }

            if (_bufferFullCounter >= 3 && previousErrorWithin10Minutes)
            {
                _logger.LogInformation("[LibraryWatcher] Internal buffer has been overflown multiple times in past 10 minutes. Suspending file watching for an hour. Restart count: {RestartCount}", _restartCounter);
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

        _logger.LogInformation("[LibraryWatcher] Folder watching has been disabled");
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
        _logger.LogTrace("[LibraryWatcher] Processing change of {FilePath}", filePath);
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
                _logger.LogTrace("[LibraryWatcher] Change from {FilePath} is not an archive or book, ignoring change", filePath);
                return;
            }

            await _taskScheduler.EnqueueScanFolderFromWatcherAsync(filePath, isDirectoryChange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LibraryWatcher] An error occured when processing a watch event");
        }
        _logger.LogTrace("[LibraryWatcher] ProcessChange completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
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
