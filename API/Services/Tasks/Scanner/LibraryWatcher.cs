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

/// <summary>
/// Change information
/// </summary>
public class Change
{
    /// <summary>
    /// Gets or sets the type of the change.
    /// </summary>
    /// <value>
    /// The type of the change.
    /// </value>
    public WatcherChangeTypes ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the full path.
    /// </summary>
    /// <value>
    /// The full path.
    /// </value>
    public string FullPath { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>
    /// The name.
    /// </value>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the old full path.
    /// </summary>
    /// <value>
    /// The old full path.
    /// </value>
    public string OldFullPath { get; set; }

    /// <summary>
    /// Gets or sets the old name.
    /// </summary>
    /// <value>
    /// The old name.
    /// </value>
    public string OldName { get; set; }
}

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

    private readonly Dictionary<string, IList<FileSystemWatcher>> _watcherDictionary = new ();
    /// <summary>
    /// This is just here to prevent GC from Disposing our watchers
    /// </summary>
    private readonly IList<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
    private IList<string> _libraryFolders = new List<string>();

    private readonly TimeSpan _queueWaitTime;


    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<LibraryWatcher> logger, IScannerService scannerService, IHostEnvironment environment)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scannerService = scannerService;

        _queueWaitTime = environment.IsDevelopment() ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(1);

    }

    public async Task StartWatching()
    {
        _logger.LogInformation("Starting file watchers");

        _libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .SelectMany(l => l.Folders)
            .Distinct()
            .Select(Parser.Parser.NormalizePath)
            .ToList();
        foreach (var libraryFolder in _libraryFolders)
        {
            _logger.LogDebug("Watching {FolderPath}", libraryFolder);
            var watcher = new FileSystemWatcher(libraryFolder);

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            _fileWatchers.Add(watcher);
            if (!_watcherDictionary.ContainsKey(libraryFolder))
            {
                _watcherDictionary.Add(libraryFolder, new List<FileSystemWatcher>());
            }

            _watcherDictionary[libraryFolder].Add(watcher);
        }
    }

    public void StopWatching()
    {
        _logger.LogInformation("Stopping watching folders");
        foreach (var fileSystemWatcher in _watcherDictionary.Values.SelectMany(watcher => watcher))
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Changed -= OnChanged;
            fileSystemWatcher.Created -= OnCreated;
            fileSystemWatcher.Deleted -= OnDeleted;
            fileSystemWatcher.Renamed -= OnRenamed;
            fileSystemWatcher.Dispose();
        }
        _fileWatchers.Clear();
        _watcherDictionary.Clear();
    }

    public async Task RestartWatching()
    {
        StopWatching();
        await StartWatching();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        _logger.LogDebug("[LibraryWatcher] Changed: {FullPath}, {Name}", e.FullPath, e.Name);
        ProcessChange(e.FullPath, string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name)));
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("[LibraryWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        ProcessChange(e.FullPath, !_directoryService.FileSystem.File.Exists(e.Name));
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
        ProcessChange(e.FullPath, true);
    }


    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "[LibraryWatcher] An error occured, likely too many watches occured at once. Restarting Watchers");
        Task.Run(RestartWatching);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        //_logger.LogDebug("[LibraryWatcher] Renamed {OldFullPath} -> {FullPath}", e.OldFullPath, e.FullPath);
        //ProcessChange(e.FullPath, _directoryService.FileSystem.Directory.Exists(e.FullPath));
    }

    /// <summary>
    /// Processes the file or folder change. If the change is a file change and not from a supported extension, it will be ignored.
    /// </summary>
    /// <param name="filePath">File or folder that changed</param>
    /// <param name="isDirectoryChange">If the change is on a directory and not a file</param>
    private void ProcessChange(string filePath, bool isDirectoryChange = false)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // We need to check if directory or not
            // !new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(filePath).Extension) (old code)
            if (!isDirectoryChange &&
                !(Parser.Parser.IsArchive(filePath) || Parser.Parser.IsBook(filePath))) return;

            var parentDirectory = _directoryService.GetParentDirectoryName(filePath);
            if (string.IsNullOrEmpty(parentDirectory)) return;

            // We need to find the library this creation belongs to
            // Multiple libraries can point to the same base folder. In this case, we need use FirstOrDefault
            var libraryFolder = _libraryFolders.FirstOrDefault(f => parentDirectory.Contains(f));
            if (string.IsNullOrEmpty(libraryFolder)) return;

            var rootFolder = _directoryService.GetFoldersTillRoot(libraryFolder, filePath).ToList();
            if (!rootFolder.Any()) return;

            // Select the first folder and join with library folder, this should give us the folder to scan.
            var fullPath =
                Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(libraryFolder, rootFolder.First()));

            var alreadyScheduled =
                TaskScheduler.HasAlreadyEnqueuedTask(ScannerService.Name, "ScanFolder", new object[] {fullPath});
            _logger.LogDebug("{FullPath} already enqueued: {Value}", fullPath, alreadyScheduled);
            if (!alreadyScheduled)
            {
                _logger.LogDebug("[LibraryWatcher] Scheduling ScanFolder for {Folder}", fullPath);
                BackgroundJob.Schedule(() => _scannerService.ScanFolder(fullPath), _queueWaitTime);
            }
            else
            {
                _logger.LogDebug("[LibraryWatcher] Skipped scheduling ScanFolder for {Folder} as a job already queued",
                    fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LibraryWatcher] An error occured when processing a watch event");
        }
        _logger.LogCritical("ProcessChange occured in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }



}
