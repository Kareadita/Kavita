using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Parser;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibrary(int libraryId, bool forceUpdate = false);

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibraries();

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true);

    Task ScanFolder(string folder);

}

public enum ScanCancelReason
{
    /// <summary>
    /// Don't cancel, everything is good
    /// </summary>
    NoCancel = 0,
    /// <summary>
    /// A folder is completely empty or missing
    /// </summary>
    FolderMount = 1,
    /// <summary>
    /// There has been no change to the filesystem since last scan
    /// </summary>
    NoChange = 2,
    /// <summary>
    /// The underlying folder is missing
    /// </summary>
    FolderMissing = 3
}

/**
 * Responsible for Scanning the disk and importing/updating/deleting files -> DB entities.
 */
public class ScannerService : IScannerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ScannerService> _logger;
    private readonly IMetadataService _metadataService;
    private readonly ICacheService _cacheService;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IProcessSeries _processSeries;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;

    public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger,
        IMetadataService metadataService, ICacheService cacheService, IEventHub eventHub,
        IDirectoryService directoryService, IReadingItemService readingItemService,
        IProcessSeries processSeries, IWordCountAnalyzerService wordCountAnalyzerService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _metadataService = metadataService;
        _cacheService = cacheService;
        _eventHub = eventHub;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _processSeries = processSeries;
        _wordCountAnalyzerService = wordCountAnalyzerService;
    }

    public async Task ScanFolder(string folder)
    {
        var seriesId = await _unitOfWork.SeriesRepository.GetSeriesIdByFolder(folder);
        if (seriesId > 0)
        {
            BackgroundJob.Enqueue(() => ScanSeries(seriesId, true));
            return;
        }

        // This is basically rework of what's already done in Library Watcher but is needed if invoked via API
        var parentDirectory = _directoryService.GetParentDirectoryName(folder);
        if (string.IsNullOrEmpty(parentDirectory)) return; // This should never happen as it's calculated before enqueing

        var libraries = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync()).ToList();
        var libraryFolders = libraries.SelectMany(l => l.Folders);
        var libraryFolder = libraryFolders.Select(Parser.Parser.NormalizePath).SingleOrDefault(f => f.Contains(parentDirectory));

        if (string.IsNullOrEmpty(libraryFolder)) return;

        var library = libraries.FirstOrDefault(l => l.Folders.Select(Parser.Parser.NormalizePath).Contains(libraryFolder));
        if (library != null)
        {
            BackgroundJob.Enqueue(() => ScanLibrary(library.Id, false));
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="bypassFolderOptimizationChecks">Not Used. Scan series will always force</param>
    [Queue(TaskScheduler.ScanQueue)]
    public async Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true)
    {
        var sw = Stopwatch.StartNew();
        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new[] {seriesId});
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId, LibraryIncludes.Folders);
        var libraryPaths = library.Folders.Select(f => f.Path).ToList();
        if (await ShouldScanSeries(seriesId, library, libraryPaths, series, true) != ScanCancelReason.NoCancel)
        {
            BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForSeries(series.LibraryId, seriesId, false));
            BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(library.Id, seriesId, false));
            return;
        }

        var folderPath = series.FolderPath;
        if (string.IsNullOrEmpty(folderPath) || !_directoryService.Exists(folderPath))
        {
            // We don't care if it's multiple due to new scan loop enforcing all in one root directory
            var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths, files.Select(f => f.FilePath).ToList());
            if (seriesDirs.Keys.Count == 0)
            {
                _logger.LogCritical("Scan Series has files spread outside a main series folder. Defaulting to library folder (this is expensive)");
                await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.InfoEvent($"{series.Name} is not organized well and scan series will be expensive!", "Scan Series has files spread outside a main series folder. Defaulting to library folder (this is expensive)"));
                seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths, files.Select(f => f.FilePath).ToList());
            }

            folderPath = seriesDirs.Keys.FirstOrDefault();

            // We should check if folderPath is a library folder path and if so, return early and tell user to correct their setup.
            if (libraryPaths.Contains(folderPath))
            {
                _logger.LogCritical("[ScannerSeries] {SeriesName} scan aborted. Files for series are not in a nested folder under library path. Correct this and rescan", series.Name);
                await _eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.ErrorEvent($"{series.Name} scan aborted", "Files for series are not in a nested folder under library path. Correct this and rescan."));
                return;
            }
        }

        if (string.IsNullOrEmpty(folderPath))
        {
            _logger.LogCritical("[ScannerSeries] Scan Series could not find a single, valid folder root for files");
            await _eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.ErrorEvent($"{series.Name} scan aborted", "Scan Series could not find a single, valid folder root for files"));
            return;
        }

        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        var processTasks = new List<Task>();


        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name));

        await _processSeries.Prime();
        void TrackFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
        {
            var parsedFiles = parsedInfo.Item2;
            if (parsedFiles.Count == 0) return;

            var foundParsedSeries = new ParsedSeries()
            {
                Name = parsedFiles.First().Series,
                NormalizedName = Parser.Parser.Normalize(parsedFiles.First().Series),
                Format = parsedFiles.First().Format
            };

            if (!foundParsedSeries.NormalizedName.Equals(series.NormalizedName))
            {
                return;
            }

            processTasks.Add(_processSeries.ProcessSeriesAsync(parsedFiles, library));
            parsedSeries.Add(foundParsedSeries, parsedFiles);
        }

        _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
        var scanElapsedTime = await ScanFiles(library, new []{folderPath}, false, TrackFiles, true);
        _logger.LogInformation("ScanFiles for {Series} took {Time}", series.Name, scanElapsedTime);

        await Task.WhenAll(processTasks);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));

        // Remove any parsedSeries keys that don't belong to our series. This can occur when users store 2 series in the same folder
        RemoveParsedInfosNotForSeries(parsedSeries, series);

         // If nothing was found, first validate any of the files still exist. If they don't then we have a deletion and can skip the rest of the logic flow
         if (parsedSeries.Count == 0)
         {
             var seriesFiles = (await _unitOfWork.SeriesRepository.GetFilesForSeries(series.Id));
             var anyFilesExist = seriesFiles.Where(f => f.FilePath.Contains(series.FolderPath)).Any(m => File.Exists(m.FilePath));

             if (!anyFilesExist)
             {
                 try
                 {
                     _unitOfWork.SeriesRepository.Remove(series);
                     await CommitAndSend(1, sw, scanElapsedTime, series);
                     await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                         MessageFactory.SeriesRemovedEvent(seriesId, string.Empty, series.LibraryId), false);
                 }
                 catch (Exception ex)
                 {
                     _logger.LogCritical(ex, "There was an error during ScanSeries to delete the series as no files could be found. Aborting scan");
                     await _unitOfWork.RollbackAsync();
                     return;
                 }
             }
             else
             {
                 // I think we should just fail and tell user to fix their setup. This is extremely expensive for an edge case
                 _logger.LogCritical("We weren't able to find any files in the series scan, but there should be. Please correct your naming convention or put Series in a dedicated folder. Aborting scan");
                 await _eventHub.SendMessageAsync(MessageFactory.Error,
                     MessageFactory.ErrorEvent($"Error scanning {series.Name}", "We weren't able to find any files in the series scan, but there should be. Please correct your naming convention or put Series in a dedicated folder. Aborting scan"));
                 await _unitOfWork.RollbackAsync();
                 return;
             }
             // At this point, parsedSeries will have at least one key and we can perform the update. If it still doesn't, just return and don't do anything
             if (parsedSeries.Count == 0) return;
         }


         await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));
        // Tell UI that this series is done
        await _eventHub.SendMessageAsync(MessageFactory.ScanSeries,
            MessageFactory.ScanSeriesEvent(library.Id, seriesId, series.Name));

        await _metadataService.RemoveAbandonedMetadataKeys();
        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.TempDirectory));
    }

    private async Task<ScanCancelReason> ShouldScanSeries(int seriesId, Library library, IList<string> libraryPaths, Series series, bool bypassFolderChecks = false)
    {
        var seriesFolderPaths = (await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId))
            .Select(f => _directoryService.FileSystem.FileInfo.FromFileName(f.FilePath).Directory.FullName)
            .Distinct()
            .ToList();

        if (!await CheckMounts(library.Name, seriesFolderPaths))
        {
            _logger.LogCritical(
                "Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return ScanCancelReason.FolderMount;
        }

        if (!await CheckMounts(library.Name, libraryPaths))
        {
            _logger.LogCritical(
                "Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return ScanCancelReason.FolderMount;
        }

        // If all series Folder paths haven't been modified since last scan, abort
        if (!bypassFolderChecks)
        {

            var allFolders = seriesFolderPaths.SelectMany(path => _directoryService.GetDirectories(path)).ToList();
            allFolders.AddRange(seriesFolderPaths);

            try
            {
                if (allFolders.All(folder => _directoryService.GetLastWriteTime(folder) <= series.LastFolderScanned))
                {
                    _logger.LogInformation(
                        "[ScannerService] {SeriesName} scan has no work to do. All folders have not been changed since last scan",
                        series.Name);
                    await _eventHub.SendMessageAsync(MessageFactory.Info,
                        MessageFactory.InfoEvent($"{series.Name} scan has no work to do",
                            "All folders have not been changed since last scan. Scan will be aborted."));
                    return ScanCancelReason.NoChange;
                }
            }
            catch (IOException ex)
            {
                // If there is an exception it means that the folder doesn't exist. So we should delete the series
                _logger.LogError(ex, "[ScannerService] Scan series for {SeriesName} found the folder path no longer exists",
                    series.Name);
                await _eventHub.SendMessageAsync(MessageFactory.Info,
                    MessageFactory.ErrorEvent($"{series.Name} scan has no work to do",
                        "The folder the series is in is missing. Delete series manually or perform a library scan."));
                return ScanCancelReason.NoCancel;
            }
        }


        return ScanCancelReason.NoCancel;
    }

    private static void RemoveParsedInfosNotForSeries(Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Series series)
    {
        var keys = parsedSeries.Keys;
        foreach (var key in keys.Where(key => !SeriesHelper.FindSeries(series, key))) // series.Format != key.Format ||
        {
            parsedSeries.Remove(key);
        }
    }

    private async Task CommitAndSend(int seriesCount, Stopwatch sw, long scanElapsedTime, Series series)
    {
        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            _logger.LogInformation(
                "Processed files and {SeriesCount} series in {ElapsedScanTime} milliseconds for {SeriesName}",
                seriesCount, sw.ElapsedMilliseconds + scanElapsedTime, series.Name);
        }
    }

    /// <summary>
    /// Ensure that all library folders are mounted. In the case that any are empty or non-existent, emit an event to the UI via EventHub and return false
    /// </summary>
    /// <param name="libraryName"></param>
    /// <param name="folders"></param>
    /// <returns></returns>
    private async Task<bool> CheckMounts(string libraryName, IList<string> folders)
    {
        // Check if any of the folder roots are not available (ie disconnected from network, etc) and fail if any of them are
        if (folders.Any(f => !_directoryService.IsDriveMounted(f)))
        {
            _logger.LogCritical("Some of the root folders for library ({LibraryName} are not accessible. Please check that drives are connected and rescan. Scan will be aborted", libraryName);

            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted",
                    string.Join(", ", folders.Where(f => !_directoryService.IsDriveMounted(f)))));

            return false;
        }


        // For Docker instances check if any of the folder roots are not available (ie disconnected volumes, etc) and fail if any of them are
        if (folders.Any(f => _directoryService.IsDirectoryEmpty(f)))
        {
            // That way logging and UI informing is all in one place with full context
            _logger.LogError("Some of the root folders for the library are empty. " +
                             "Either your mount has been disconnected or you are trying to delete all series in the library. " +
                             "Scan has be aborted. " +
                             "Check that your mount is connected or change the library's root folder and rescan");

            await _eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.ErrorEvent( $"Some of the root folders for the library, {libraryName}, are empty.",
                "Either your mount has been disconnected or you are trying to delete all series in the library. " +
                "Scan has be aborted. " +
                "Check that your mount is connected or change the library's root folder and rescan"));

            return false;
        }

        return true;
    }

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibraries()
    {
        _logger.LogInformation("Starting Scan of All Libraries");
        foreach (var lib in await _unitOfWork.LibraryRepository.GetLibrariesAsync())
        {
            await ScanLibrary(lib.Id);
        }
        _logger.LogInformation("Scan of All Libraries Finished");
    }


    /// <summary>
    /// Scans a library for file changes.
    /// Will kick off a scheduled background task to refresh metadata,
    /// ie) all entities will be rechecked for new cover images and comicInfo.xml changes
    /// </summary>
    /// <param name="libraryId"></param>
    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibrary(int libraryId, bool forceUpdate = false)
    {
        var sw = Stopwatch.StartNew();
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        var libraryFolderPaths = library.Folders.Select(fp => fp.Path).ToList();
        if (!await CheckMounts(library.Name, libraryFolderPaths)) return;

        // If all library Folder paths haven't been modified since last scan, abort
        // Unless the user did something on the library (delete series) and thus we can bypass this check
        var wasLibraryUpdatedSinceLastScan = (library.LastModified.Truncate(TimeSpan.TicksPerMinute) >
                                             library.LastScanned.Truncate(TimeSpan.TicksPerMinute))
                                             && library.LastScanned != DateTime.MinValue;
        if (!forceUpdate && !wasLibraryUpdatedSinceLastScan)
        {
            var haveFoldersChangedSinceLastScan = library.Folders
                .All(f => _directoryService.GetLastWriteTime(f.Path).Truncate(TimeSpan.TicksPerMinute) > f.LastScanned.Truncate(TimeSpan.TicksPerMinute));

            // If nothing changed && library folder's have all been scanned at least once
            if (!haveFoldersChangedSinceLastScan && library.Folders.All(f => f.LastScanned > DateTime.MinValue))
            {
                _logger.LogInformation("[ScannerService] {LibraryName} scan has no work to do. All folders have not been changed since last scan", library.Name);
                await _eventHub.SendMessageAsync(MessageFactory.Info,
                    MessageFactory.InfoEvent($"{library.Name} scan has no work to do",
                        "All folders have not been changed since last scan. Scan will be aborted."));

                BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForLibrary(library.Id, false));
                BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanLibrary(library.Id, false));
                return;
            }
        }


        // Validations are done, now we can start actual scan
        _logger.LogInformation("[ScannerService] Beginning file scan on {LibraryName}", library.Name);

        // This doesn't work for something like M:/Manga/ and a series has library folder as root
        var shouldUseLibraryScan = !(await _unitOfWork.LibraryRepository.DoAnySeriesFoldersMatch(libraryFolderPaths));
        if (!shouldUseLibraryScan)
        {
            _logger.LogError("Library {LibraryName} consists of one or more Series folders, using series scan", library.Name);
        }


        var totalFiles = 0;
        var seenSeries = new List<ParsedSeries>();


        await _processSeries.Prime();
        var processTasks = new List<Task>();
        void TrackFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
        {
            var skippedScan = parsedInfo.Item1;
            var parsedFiles = parsedInfo.Item2;
            if (parsedFiles.Count == 0) return;

            var foundParsedSeries = new ParsedSeries()
            {
                Name = parsedFiles.First().Series,
                NormalizedName = Parser.Parser.Normalize(parsedFiles.First().Series),
                Format = parsedFiles.First().Format
            };

            if (skippedScan)
            {
                seenSeries.AddRange(parsedFiles.Select(pf => new ParsedSeries()
                {
                    Name = pf.Series,
                    NormalizedName = Parser.Parser.Normalize(pf.Series),
                    Format = pf.Format
                }));
                return;
            }

            totalFiles += parsedFiles.Count;


            seenSeries.Add(foundParsedSeries);
            processTasks.Add(_processSeries.ProcessSeriesAsync(parsedFiles, library));
        }


        var scanElapsedTime = await ScanFiles(library, libraryFolderPaths, shouldUseLibraryScan, TrackFiles);


        await Task.WhenAll(processTasks);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(string.Empty, library.Name, ProgressEventType.Ended));

        _logger.LogInformation("[ScannerService] Finished file scan in {ScanAndUpdateTime}. Updating database", scanElapsedTime);

        var time = DateTime.Now;
        foreach (var folderPath in library.Folders)
        {
            folderPath.LastScanned = time;
        }

        library.LastScanned = time;

        // Could I delete anything in a Library's Series where the LastScan date is before scanStart?
        // NOTE: This implementation is expensive
        await _unitOfWork.SeriesRepository.RemoveSeriesNotInList(seenSeries, library.Id);

        _unitOfWork.LibraryRepository.Update(library);
        if (await _unitOfWork.CommitAsync())
        {
            _logger.LogInformation(
                "[ScannerService] Finished scan of {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                totalFiles, seenSeries.Count, sw.ElapsedMilliseconds, library.Name);
        }
        else
        {
            _logger.LogCritical(
                "[ScannerService] There was a critical error that resulted in a failed scan. Please check logs and rescan");
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, string.Empty));
        await _metadataService.RemoveAbandonedMetadataKeys();

        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.TempDirectory));
    }

    private async Task<long> ScanFiles(Library library, IEnumerable<string> dirs,
        bool isLibraryScan, Action<Tuple<bool, IList<ParserInfo>>> processSeriesInfos = null, bool forceChecks = false)
    {
        var scanner = new ParseScannedFiles(_logger, _directoryService, _readingItemService, _eventHub);
        var scanWatch = Stopwatch.StartNew();

        await scanner.ScanLibrariesForSeries(library.Type, dirs, library.Name,
            isLibraryScan, await _unitOfWork.SeriesRepository.GetFolderPathMap(library.Id), processSeriesInfos, forceChecks);

        var scanElapsedTime = scanWatch.ElapsedMilliseconds;

        return scanElapsedTime;
    }

    /// <summary>
    /// Remove any user progress rows that no longer exist since scan library ran and deleted series/volumes/chapters
    /// </summary>
    private async Task CleanupAbandonedChapters()
    {
        var cleanedUp = await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
        _logger.LogInformation("Removed {Count} abandoned progress rows", cleanedUp);
    }


    /// <summary>
    /// Cleans up any abandoned rows due to removals from Scan loop
    /// </summary>
    private async Task CleanupDbEntities()
    {
        await CleanupAbandonedChapters();
        var cleanedUp = await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
        _logger.LogInformation("Removed {Count} abandoned collection tags", cleanedUp);
    }

    public static IEnumerable<Series> FindSeriesNotOnDisk(IEnumerable<Series> existingSeries, Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries)
    {
        return existingSeries.Where(es => !ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(es, parsedSeries));
    }
}
