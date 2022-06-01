using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Data;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;

public interface ILibraryWatcher
{
    Task StartWatchingLibraries();
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

    private IList<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

    private Dictionary<string, IList<FileSystemWatcher>> _watcherDictionary =
        new Dictionary<string, IList<FileSystemWatcher>>();

    private IList<string> _libraryFolders = new List<string>();

    // TODO: This needs to be blocking so we can consume from another thread
    private readonly Queue<FolderScanQueueable> _scanQueue = new Queue<FolderScanQueueable>();



    public LibraryWatcher(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<LibraryWatcher> logger, IScannerService scannerService)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scannerService = scannerService;
    }

    public async Task StartWatchingLibraries()
    {
        _logger.LogInformation("Starting file watchers");
        _libraryFolders = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync()).SelectMany(l => l.Folders).ToList();

        foreach (var library in await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
        {
            foreach (var libraryFolder in library.Folders)
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
                _logger.LogInformation("Watching {Folder}", libraryFolder);
                _watchers.Add(watcher);
                if (!_watcherDictionary.ContainsKey(libraryFolder))
                {
                    _watcherDictionary.Add(libraryFolder, new List<FileSystemWatcher>());
                }

                _watcherDictionary[libraryFolder].Add(watcher);
            }
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        if (!new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(e.FullPath).Extension)) return;
        ProcessQueue();
        Console.WriteLine($"Changed: {e.FullPath}, {e.Name}");
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        // TODO: This needs to enqueue a job that runs in 5 mins or so (as multiple files can be processed).
        if (!new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(e.FullPath).Extension)) return;

        var attr = File.GetAttributes(e.FullPath);
        var isDirectory = attr.HasFlag(FileAttributes.Directory);

        string parentDirectory = string.Empty;
        if (isDirectory)
        {
            parentDirectory = Parser.Parser.NormalizePath(_directoryService.FileSystem.DirectoryInfo.FromDirectoryName(e.FullPath).Parent
                .FullName);
        }
        else
        {
            parentDirectory = Parser.Parser.NormalizePath(_directoryService.FileSystem.FileInfo.FromFileName(e.FullPath).Directory.Parent
                .FullName);
        }

        //var directory = _directoryService.FileSystem.FileInfo.FromFileName(e.FullPath).DirectoryName; //.Directory.Parent;
        // We need to find the library this creation belongs to
        var libraryFolder = _libraryFolders.Select(Parser.Parser.NormalizePath).SingleOrDefault(f => f.Contains(parentDirectory));

        if (string.IsNullOrEmpty(libraryFolder)) return;

        var rootFolder = _directoryService.GetFoldersTillRoot(libraryFolder, e.FullPath).ToList();
        if (!rootFolder.Any()) return;

        // Select the first folder and join with library folder, this should give us the folder to scan.
        var fullPath = _directoryService.FileSystem.Path.Join(libraryFolder, rootFolder.First());
        var queueItem = new FolderScanQueueable()
        {
            FolderPath = fullPath,
            QueueTime = DateTime.Now
        };
        if (_scanQueue.Contains(queueItem, new FolderScanQueueableComparer()))
        {
            ProcessQueue();
            return;
        }

        _scanQueue.Enqueue(queueItem);

        ProcessQueue();

        Console.WriteLine($"Created: {e.FullPath}, {e.Name}");
    }

    private void OnDeleted(object sender, FileSystemEventArgs e) {
        if (!new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(e.FullPath).Extension)) return;
        ProcessQueue();
        Console.WriteLine($"Deleted: {e.FullPath}, {e.Name}");
    }



    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!new Regex(Parser.Parser.SupportedExtensions).IsMatch(new FileInfo(e.FullPath).Extension)) return;
        ProcessQueue();
        Console.WriteLine($"Renamed:");
        Console.WriteLine($"    Old: {e.OldFullPath}");
        Console.WriteLine($"    New: {e.FullPath}");
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
            if (item.QueueTime < DateTime.Now.Subtract(TimeSpan.FromSeconds(10))) // TimeSpan.FromMinutes(5)
            {
                BackgroundJob.Enqueue(() => _scannerService.ScanSeriesFolder(item.FolderPath));
                _scanQueue.Dequeue();
                i++;
            }
            else
            {
                break;
            }
        }
    }
}
