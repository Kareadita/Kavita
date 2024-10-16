using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;
#nullable enable

public interface ILibraryWatcher
{
    /// <summary>
    /// Start watching all library folders
    /// </summary>
    /// <returns></returns>
    Task StartWatching();
    /// <summary>
    /// Stop watching all folders
    /// </summary>
    void StopWatching();
    /// <summary>
    /// Essentially stops then starts watching. Useful if there is a change in folders or libraries
    /// </summary>
    /// <returns></returns>
    Task RestartWatching();
}

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

    private static readonly Dictionary<string, IList<FileSystemWatcher>> WatcherDictionary = new ();
    /// <summary>
    /// This is just here to prevent GC from Disposing our watchers
    /// </summary>
    private static readonly IList<FileSystemWatcher> FileWatchers = new List<FileSystemWatcher>();
    /// <summary>
    /// The amount of time until the Schedule ScanFolder task should be executed
    /// </summary>
    /// <remarks>The Job will be enqueued instantly</remarks>
    private readonly TimeSpan _queueWaitTime;

    /// <summary>
    /// Counts within a time frame how many times the buffer became full. Is used to reschedule LibraryWatcher to start monitoring much later rather than instantly
    /// </summary>
    private static int _bufferFullCounter;
    private static int _restartCounter;
    private static DateTime _lastErrorTime = DateTime.MinValue;
    /// <summary>
    /// Used to lock buffer Full Counter
    /// </summary>
    private static readonly object Lock = new ();

    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork,
        ILogger<LibraryWatcher> logger, IHostEnvironment environment, ITaskScheduler taskScheduler)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _taskScheduler = taskScheduler;

        _queueWaitTime = environment.IsDevelopment() ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);

    }

    public async Task StartWatching()
    {
        FileWatchers.Clear();
        WatcherDictionary.Clear();

        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableFolderWatching)
        {
            _logger.LogInformation("Folder watching is disabled at the server level, thus ignoring any requests to create folder watching");
            return;
        }

        var libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .Where(l => l.FolderWatching)
            .SelectMany(l => l.Folders)
            .Distinct()
            .Select(Parser.Parser.NormalizePath)
            .Where(_directoryService.Exists)
            .ToList();

        _logger.LogInformation("[LibraryWatcher] Starting file watchers for {Count} library folders", libraryFolders.Count);

        foreach (var libraryFolder in libraryFolders)
        {
            _logger.LogDebug("[LibraryWatcher] Watching {FolderPath}", libraryFolder);
            var watcher = new FileSystemWatcher(libraryFolder);

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Error += OnError;
            watcher.Disposed += (_, _) =>
                _logger.LogError("[LibraryWatcher] watcher was disposed when it shouldn't have been. Please report this to Kavita dev");

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            FileWatchers.Add(watcher);
            if (!WatcherDictionary.ContainsKey(libraryFolder))
            {
                WatcherDictionary.Add(libraryFolder, new List<FileSystemWatcher>());
            }

            WatcherDictionary[libraryFolder].Add(watcher);
        }
        _logger.LogInformation("[LibraryWatcher] Watching {Count} folders", libraryFolders.Count);
    }

    public void StopWatching()
    {
        _logger.LogInformation("[LibraryWatcher] Stopping watching folders");
        foreach (var fileSystemWatcher in WatcherDictionary.Values.SelectMany(watcher => watcher))
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Changed -= OnChanged;
            fileSystemWatcher.Created -= OnCreated;
            fileSystemWatcher.Deleted -= OnDeleted;
            fileSystemWatcher.Error -= OnError;
        }
        FileWatchers.Clear();
        WatcherDictionary.Clear();
    }

    public async Task RestartWatching()
    {
        _logger.LogDebug("[LibraryWatcher] Restarting watcher");

        StopWatching();
        await StartWatching();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[LibraryWatcher] Changed: {FullPath}, {Name}, {ChangeType}", e.FullPath, e.Name, e.ChangeType);
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        var isDirectoryChange = string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name));

        if (TaskScheduler.HasAlreadyEnqueuedTask("LibraryWatcher", "ProcessChange", [e.FullPath, isDirectoryChange],
                checkRunningJobs: true))
        {
            return;
        }

        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, isDirectoryChange));
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogTrace("[LibraryWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        var isDirectoryChange = !_directoryService.FileSystem.File.Exists(e.Name);
        if (TaskScheduler.HasAlreadyEnqueuedTask("LibraryWatcher", "ProcessChange", [e.FullPath, isDirectoryChange],
                checkRunningJobs: true))
        {
            return;
        }
        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, isDirectoryChange));
    }

    /// <summary>
    /// From testing, on Deleted only needs to pass through the event when a folder is deleted. If a file is deleted, Changed will handle automatically.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDeleted(object sender, FileSystemEventArgs e) {
        var isDirectory = string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name));
        if (!isDirectory) return;
        _logger.LogTrace("[LibraryWatcher] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        if (TaskScheduler.HasAlreadyEnqueuedTask("LibraryWatcher", "ProcessChange", [e.FullPath, true],
                checkRunningJobs: true))
        {
            return;
        }
        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, true));
    }

    /// <summary>
    /// On error, we count the number of errors that have occured. If the number of errors has been more than 2 in last 10 minutes, then we suspend listening for an hour
    /// </summary>
    /// <remarks>This will schedule jobs to decrement the buffer full counter</remarks>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "[LibraryWatcher] An error occured, likely too many changes occured at once or the folder being watched was deleted. Restarting Watchers {Current}/{Total}", _bufferFullCounter, 3);
        bool condition;
        lock (Lock)
        {
            _bufferFullCounter += 1;
            _lastErrorTime = DateTime.Now;
            condition = _bufferFullCounter >= 3 && (DateTime.Now - _lastErrorTime).TotalMinutes <= 10;
        }

        if (_restartCounter >= 3)
        {
            _logger.LogInformation("[LibraryWatcher] Too many restarts occured, you either have limited inotify or an OS constraint. Kavita will turn off folder watching to prevent high utilization of resources");
            Task.Run(TurnOffWatching);
            return;
        }

        if (condition)
        {
            _logger.LogInformation("[LibraryWatcher] Internal buffer has been overflown multiple times in past 10 minutes. Suspending file watching for an hour. Restart count: {RestartCount}", _restartCounter);
            _restartCounter++;
            StopWatching();
            BackgroundJob.Schedule(() => RestartWatching(), TimeSpan.FromHours(1));
            return;
        }
        Task.Run(RestartWatching);
        BackgroundJob.Schedule(() => UpdateLastBufferOverflow(), TimeSpan.FromMinutes(10));
    }

    private async Task TurnOffWatching()
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
            if (Parser.Parser.HasBlacklistedFolderInPath(filePath))
            {
                return;
            }

            // If not a directory change AND file is not an archive or book, ignore
            if (!isDirectoryChange &&
                !(Parser.Parser.IsArchive(filePath) || Parser.Parser.IsBook(filePath)))
            {
                _logger.LogTrace("[LibraryWatcher] Change from {FilePath} is not an archive or book, ignoring change", filePath);
                return;
            }

            var libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
                .SelectMany(l => l.Folders)
                .Distinct()
                .Select(Parser.Parser.NormalizePath)
                .Where(_directoryService.Exists)
                .ToList();

            var fullPath = GetFolder(filePath, libraryFolders);
            _logger.LogTrace("Folder path: {FolderPath}", fullPath);
            if (string.IsNullOrEmpty(fullPath))
            {
                _logger.LogInformation("[LibraryWatcher] Change from {FilePath} could not find root level folder, ignoring change", filePath);
                return;
            }

            _taskScheduler.ScanFolder(fullPath, filePath, _queueWaitTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LibraryWatcher] An error occured when processing a watch event");
        }
        _logger.LogTrace("[LibraryWatcher] ProcessChange completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }

    private string GetFolder(string filePath, IEnumerable<string> libraryFolders)
    {
        // TODO: I can optimize this to avoid a library scan and instead do a Series Scan by finding the series that has a lowestFolderPath higher or equal to the filePath

        var parentDirectory = _directoryService.GetParentDirectoryName(filePath);
        _logger.LogTrace("[LibraryWatcher] Parent Directory: {ParentDirectory}", parentDirectory);
        if (string.IsNullOrEmpty(parentDirectory)) return string.Empty;

        // We need to find the library this creation belongs to
        // Multiple libraries can point to the same base folder. In this case, we need use FirstOrDefault
        var libraryFolder = libraryFolders.FirstOrDefault(f => parentDirectory.Contains(f));
        _logger.LogTrace("[LibraryWatcher] Library Folder: {LibraryFolder}", libraryFolder);
        if (string.IsNullOrEmpty(libraryFolder)) return string.Empty;

        var rootFolder = _directoryService.GetFoldersTillRoot(libraryFolder, filePath).ToList();
        _logger.LogTrace("[LibraryWatcher] Root Folders: {RootFolders}", rootFolder);
        if (rootFolder.Count == 0) return string.Empty;

        // Select the first folder and join with library folder, this should give us the folder to scan.
        return Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(libraryFolder, rootFolder[rootFolder.Count - 1]));
    }


    /// <summary>
    /// This is called via Hangfire to decrement the counter. Must work around a lock
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public static void UpdateLastBufferOverflow()
    {
        lock (Lock)
        {
            if (_bufferFullCounter == 0) return;
            _bufferFullCounter -= 1;
        }
    }
}
