using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

internal class FolderScanQueueable
{
    public DateTime QueueTime { get; set; }
    public string FolderPath { get; set; }
}

internal class FolderScanQueueableComparer : IEqualityComparer<FolderScanQueueable>
{
    public bool Equals(FolderScanQueueable x, FolderScanQueueable y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.FolderPath == y.FolderPath;
    }

    public int GetHashCode(FolderScanQueueable obj)
    {
        return HashCode.Combine(obj.FolderPath);
    }
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
    private IList<string> _libraryFolders = new List<string>();

    private readonly Queue<FolderScanQueueable> _scanQueue = new Queue<FolderScanQueueable>();
    private readonly TimeSpan _queueWaitTime;
    private readonly FolderScanQueueableComparer _folderScanQueueableComparer = new FolderScanQueueableComparer();


    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<LibraryWatcher> logger, IScannerService scannerService, IHostEnvironment environment)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scannerService = scannerService;

        _queueWaitTime = environment.IsDevelopment() ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(1);

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
            watcher.NotifyFilter =   NotifyFilters.CreationTime
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
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
        ProcessChange(e.FullPath);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("[LibraryWatcher] Created: {FullPath}, {Name}", e.FullPath, e.Name);
        ProcessChange(e.FullPath, !_directoryService.FileSystem.File.Exists(e.Name));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e) {
        _logger.LogDebug("[LibraryWatcher] Deleted: {FullPath}, {Name}", e.FullPath, e.Name);

        // On deletion, we need another type of check. We need to check if e.Name has an extension or not
        // NOTE: File deletion will trigger a folder change event, so this might not be needed
        ProcessChange(e.FullPath, string.IsNullOrEmpty(_directoryService.FileSystem.Path.GetExtension(e.Name)));
    }



    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug($"[LibraryWatcher] Renamed:");
        _logger.LogDebug("    Old: {OldFullPath}", e.OldFullPath);
        _logger.LogDebug("    New: {FullPath}", e.FullPath);
        ProcessChange(e.FullPath, _directoryService.FileSystem.Directory.Exists(e.FullPath));
    }

    /// <summary>
    /// Processes the file or folder change.
    /// </summary>
    /// <param name="filePath">File or folder that changed</param>
    /// <param name="isDirectoryChange">If the change is on a directory and not a file</param>
    private void ProcessChange(string filePath, bool isDirectoryChange = false)
    {
        // We need to check if directory or not
        if (!isDirectoryChange && !new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(filePath).Extension)) return;

        var parentDirectory = _directoryService.GetParentDirectoryName(filePath);
        if (string.IsNullOrEmpty(parentDirectory)) return;

        // We need to find the library this creation belongs to
        // Multiple libraries can point to the same base folder. In this case, we need use FirstOrDefault
        var libraryFolder = _libraryFolders.FirstOrDefault(f => parentDirectory.Contains(f));
        if (string.IsNullOrEmpty(libraryFolder)) return;

        var rootFolder = _directoryService.GetFoldersTillRoot(libraryFolder, filePath).ToList();
        if (!rootFolder.Any()) return;

        // Select the first folder and join with library folder, this should give us the folder to scan.
        var fullPath = Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(libraryFolder, rootFolder.First()));
        var queueItem = new FolderScanQueueable()
        {
            FolderPath = fullPath,
            QueueTime = DateTime.Now
        };
        if (!_scanQueue.Contains(queueItem, _folderScanQueueableComparer))
        {
            _logger.LogDebug("[LibraryWatcher] Queuing job for {Folder}", fullPath);
            _scanQueue.Enqueue(queueItem);
        }

        ProcessQueue();
    }

    /// <summary>
    /// Instead of making things complicated with a separate thread, this service will process the queue whenever a change occurs
    /// </summary>
    private void ProcessQueue()
    {
        var i = 0;
        while (i < _scanQueue.Count)
        {
            var item = _scanQueue.Peek();
            if (item.QueueTime < DateTime.Now.Subtract(_queueWaitTime))
            {
                _logger.LogDebug("[LibraryWatcher] Scheduling ScanSeriesFolder for {Folder}", item.FolderPath);
                BackgroundJob.Enqueue(() => _scannerService.ScanFolder(item.FolderPath));
                _scanQueue.Dequeue();
                i++;
            }
            else
            {
                break;
            }
        }

        if (_scanQueue.Count > 0)
        {
            Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t=> ProcessQueue());
        }

    }
}
