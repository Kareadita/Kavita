using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;

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
    private readonly IScannerService _scannerService;

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
    private int _bufferFullCounter = 0;

    private DateTime _lastBufferOverflow = DateTime.MinValue;



    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<LibraryWatcher> logger, IScannerService scannerService, IHostEnvironment environment)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scannerService = scannerService;

        _queueWaitTime = environment.IsDevelopment() ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);

    }

    public async Task StartWatching()
    {
        _logger.LogInformation("[LibraryWatcher] Starting file watchers");

        var libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .SelectMany(l => l.Folders)
            .Distinct()
            .Select(Parser.Parser.NormalizePath)
            .Where(_directoryService.Exists)
            .ToList();

        foreach (var libraryFolder in libraryFolders)
        {
            _logger.LogDebug("[LibraryWatcher] Watching {FolderPath}", libraryFolder);
            var watcher = new FileSystemWatcher(libraryFolder);

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Error += OnError;
            watcher.Disposed += (sender, args) =>
                _logger.LogError("[LibraryWatcher] watcher was disposed when it shouldn't have been");

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
        _logger.LogInformation("[LibraryWatcher] Watching {Count} folders", FileWatchers.Count);
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

        UpdateBufferOverflow();

        StopWatching();
        await StartWatching();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        _logger.LogDebug("[LibraryWatcher] Changed: {FullPath}, {Name}", e.FullPath, e.Name);
        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name))));
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("[LibraryWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, !_directoryService.FileSystem.File.Exists(e.Name)));
    }

    /// <summary>
    /// From testing, on Deleted only needs to pass through the event when a folder is deleted. If a file is deleted, Changed will handle automatically.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDeleted(object sender, FileSystemEventArgs e) {
        var isDirectory = string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name));
        if (!isDirectory) return;
        _logger.LogDebug("[LibraryWatcher] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);
        BackgroundJob.Enqueue(() => ProcessChange(e.FullPath, true));
    }


    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "[LibraryWatcher] An error occured, likely too many changes occured at once or the folder being watched was deleted. Restarting Watchers");
        _bufferFullCounter += 1;
        _lastBufferOverflow = DateTime.Now;

        if (_bufferFullCounter >= 3)
        {
            _logger.LogInformation("[LibraryWatcher] Internal buffer has been overflown multiple times in past 10 minutes. Suspending file watching for an hour");
            BackgroundJob.Schedule(() => RestartWatching(), TimeSpan.FromHours(1));
            return;
        }
        Task.Run(RestartWatching);
    }


    /// <summary>
    /// Processes the file or folder change. If the change is a file change and not from a supported extension, it will be ignored.
    /// </summary>
    /// <remarks>This will ignore image files that are added to the system. However, they may still trigger scans due to folder changes.</remarks>
    /// <remarks>This is public only because Hangfire will invoke it. Do not call external to this class.</remarks>
    /// <param name="filePath">File or folder that changed</param>
    /// <param name="isDirectoryChange">If the change is on a directory and not a file</param>
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task ProcessChange(string filePath, bool isDirectoryChange = false)
    {
        UpdateBufferOverflow();

        var sw = Stopwatch.StartNew();
        _logger.LogDebug("[LibraryWatcher] Processing change of {FilePath}", filePath);
        try
        {
            // If not a directory change AND file is not an archive or book, ignore
            if (!isDirectoryChange &&
                !(Parser.Parser.IsArchive(filePath) || Parser.Parser.IsBook(filePath)))
            {
                _logger.LogDebug("[LibraryWatcher] Change from {FilePath} is not an archive or book, ignoring change", filePath);
                return;
            }

            var libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
                .SelectMany(l => l.Folders)
                .Distinct()
                .Select(Parser.Parser.NormalizePath)
                .Where(_directoryService.Exists)
                .ToList();

            var fullPath = GetFolder(filePath, libraryFolders);
            _logger.LogDebug("Folder path: {FolderPath}", fullPath);
            if (string.IsNullOrEmpty(fullPath))
            {
                _logger.LogDebug("[LibraryWatcher] Change from {FilePath} could not find root level folder, ignoring change", filePath);
                return;
            }

            // Check if this task has already enqueued or is being processed, before enqueing

            var alreadyScheduled =
                TaskScheduler.HasAlreadyEnqueuedTask(ScannerService.Name, "ScanFolder", new object[] {fullPath});
            if (!alreadyScheduled)
            {
                _logger.LogInformation("[LibraryWatcher] Scheduling ScanFolder for {Folder}", fullPath);
                BackgroundJob.Schedule(() => _scannerService.ScanFolder(fullPath), _queueWaitTime);
            }
            else
            {
                _logger.LogInformation("[LibraryWatcher] Skipped scheduling ScanFolder for {Folder} as a job already queued",
                    fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LibraryWatcher] An error occured when processing a watch event");
        }
        _logger.LogDebug("[LibraryWatcher] ProcessChange ran in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }

    private string GetFolder(string filePath, IList<string> libraryFolders)
    {
        var parentDirectory = _directoryService.GetParentDirectoryName(filePath);
        _logger.LogDebug("[LibraryWatcher] Parent Directory: {ParentDirectory}", parentDirectory);
        if (string.IsNullOrEmpty(parentDirectory)) return string.Empty;

        // We need to find the library this creation belongs to
        // Multiple libraries can point to the same base folder. In this case, we need use FirstOrDefault
        var libraryFolder = libraryFolders.FirstOrDefault(f => parentDirectory.Contains(f));
        _logger.LogDebug("[LibraryWatcher] Library Folder: {LibraryFolder}", libraryFolder);
        if (string.IsNullOrEmpty(libraryFolder)) return string.Empty;

        var rootFolder = _directoryService.GetFoldersTillRoot(libraryFolder, filePath).ToList();
        _logger.LogDebug("[LibraryWatcher] Root Folders: {RootFolders}", rootFolder);
        if (!rootFolder.Any()) return string.Empty;

        // Select the first folder and join with library folder, this should give us the folder to scan.
        return  Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(libraryFolder, rootFolder.First()));
    }

    private void UpdateBufferOverflow()
    {
        if (_bufferFullCounter == 0) return;
        // If the last buffer overflow is over 5 mins back, we can remove a buffer count
        if (_lastBufferOverflow < DateTime.Now.Subtract(TimeSpan.FromMinutes(5)))
        {
            _bufferFullCounter = Math.Min(0, _bufferFullCounter - 1);
            _lastBufferOverflow = DateTime.Now;
        }
    }
}
