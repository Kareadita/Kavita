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

        _queueWaitTime = environment.IsDevelopment() ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(5);

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
            _logger.LogInformation("Watching {FolderPath}", libraryFolder);
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

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        Console.WriteLine($"Changed: {e.FullPath}, {e.Name}");
        ProcessChange(e.FullPath);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Created: {e.FullPath}, {e.Name}");
        ProcessChange(e.FullPath);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e) {
        Console.WriteLine($"Deleted: {e.FullPath}, {e.Name}");
        ProcessChange(e.FullPath);
    }



    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine($"Renamed:");
        Console.WriteLine($"    Old: {e.OldFullPath}");
        Console.WriteLine($"    New: {e.FullPath}");
        ProcessChange(e.FullPath);
    }

    private void ProcessChange(string filePath)
    {
        if (!new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(filePath).Extension)) return;
        // Don't do anything if a Library or ScanSeries in progress
        if (TaskScheduler.RunningAnyTasksByMethod(new[] {"MetadataService", "ScannerService"}))
        {
            _logger.LogDebug("Suppressing Change due to scan being inprogress");
            return;
        }


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
        if (_scanQueue.Contains(queueItem, _folderScanQueueableComparer))
        {
            ProcessQueue();
            return;
        }

        _scanQueue.Enqueue(queueItem);

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
                _logger.LogDebug("Scheduling ScanSeriesFolder for {Folder}", item.FolderPath);
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
            Task.Delay(_queueWaitTime).ContinueWith(t=> ProcessQueue());
        }

    }
}
