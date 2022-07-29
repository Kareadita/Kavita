using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Parser;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibrary(int libraryId);
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibraries();
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanSeries(int seriesId, CancellationToken token);
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanFolder(string folder);

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
    private readonly IFileService _fileService;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly ICacheHelper _cacheHelper;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;
    private readonly IProcessSeries _processSeries;

    public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger,
        IMetadataService metadataService, ICacheService cacheService, IEventHub eventHub,
        IFileService fileService, IDirectoryService directoryService, IReadingItemService readingItemService,
        ICacheHelper cacheHelper, IWordCountAnalyzerService wordCountAnalyzerService, IProcessSeries processSeries)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _metadataService = metadataService;
        _cacheService = cacheService;
        _eventHub = eventHub;
        _fileService = fileService;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _cacheHelper = cacheHelper;
        _wordCountAnalyzerService = wordCountAnalyzerService;
        _processSeries = processSeries;
    }

    public async Task ScanFolder(string folder)
    {
        // NOTE: I might want to move a lot of this code to the LibraryWatcher or something and just pack libraryId and seriesId
        // Validate if we are scanning a new series (that belongs to a library) or an existing series
        var seriesId = await _unitOfWork.SeriesRepository.GetSeriesIdByFolder(folder);
        if (seriesId > 0)
        {
            BackgroundJob.Enqueue(() => ScanSeries(seriesId, CancellationToken.None));
            return;
        }

        var parentDirectory = _directoryService.GetParentDirectoryName(folder);
        if (string.IsNullOrEmpty(parentDirectory)) return; // This should never happen as it's calculated before enqueing

        var libraries = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync()).ToList();
        var libraryFolders = libraries.SelectMany(l => l.Folders);
        var libraryFolder = libraryFolders.Select(Parser.Parser.NormalizePath).SingleOrDefault(f => f.Contains(parentDirectory));

        if (string.IsNullOrEmpty(libraryFolder)) return;

        var library = libraries.FirstOrDefault(l => l.Folders.Select(Parser.Parser.NormalizePath).Contains(libraryFolder));
        if (library != null)
        {
            BackgroundJob.Enqueue(() => ScanLibrary(library.Id));
        }
    }

    public async Task ScanSeries(int seriesId, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new[] {seriesId});
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId, LibraryIncludes.Folders);
        var libraryPaths = library.Folders.Select(f => f.Path).ToList();
        if (!await ShouldScanSeries(seriesId, library, libraryPaths, series)) return;


        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        var totalFiles = 0; // TODO: Figure this out

        // var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
        // var allGenres = await _unitOfWork.GenreRepository.GetAllGenresAsync();
        // var allTags = await _unitOfWork.TagRepository.GetAllTagsAsync();
        var allPeople = new BlockingCollection<Person>();;
        foreach (var person in await _unitOfWork.PersonRepository.GetAllPeople())
        {
            allPeople.Add(person);
        }

        var allGenres = new BlockingCollection<Genre>();
        foreach (var genre in await _unitOfWork.GenreRepository.GetAllGenresAsync())
        {
            allGenres.Add(genre);
        }
        var allTags = new BlockingCollection<Tag>();
        foreach (var tag in await _unitOfWork.TagRepository.GetAllTagsAsync())
        {
            allTags.Add(tag);
        }

        // TODO: Hook in folderpath optimization _directoryService.Exists(series.FolderPath)

        var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths, files.Select(f => f.FilePath).ToList());
        if (seriesDirs.Keys.Count == 0)
        {
            _logger.LogCritical("Scan Series has files spread outside a main series folder. Defaulting to library folder (this is expensive)");
            // TODO: We should send an INFO to the UI to inform user
            seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths, files.Select(f => f.FilePath).ToList());
        }

        _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
        var scanElapsedTime = await ScanFiles(library, seriesDirs.Keys, false);
        _logger.LogInformation("ScanFiles for {Series} took {Time}", series.Name, scanElapsedTime);


        // Remove any parsedSeries keys that don't belong to our series. This can occur when users store 2 series in the same folder
        RemoveParsedInfosNotForSeries(parsedSeries, series);

        // If nothing was found, first validate any of the files still exist. If they don't then we have a deletion and can skip the rest of the logic flow
        if (parsedSeries.Count == 0)
        {
            var anyFilesExist =
                (await _unitOfWork.SeriesRepository.GetFilesForSeries(series.Id)).Any(m => File.Exists(m.FilePath));

            if (!anyFilesExist)
            {
                try
                {
                    _unitOfWork.SeriesRepository.Remove(series);
                    await CommitAndSend(totalFiles, parsedSeries, sw, scanElapsedTime, series);
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
                // TODO: I think we should just fail and tell user to fix their setup. This is extremely expensive for an edge case
                _logger.LogCritical("We weren't able to find any files in the series scan, but there should be. Please correct your naming convention or put Series in a dedicated folder. Aborting scan");
                await _eventHub.SendMessageAsync(MessageFactory.Error,
                    MessageFactory.ErrorEvent("We weren't able to find any files in the series scan, but there should be. Please correct your naming convention or put Series in a dedicated folder. Aborting scan",
                        series.Name));
                await _unitOfWork.RollbackAsync();

                return;

                // We need to do an additional check for an edge case: If the scan ran and the files do not match the existing Series name, then it is very likely,
                // the files have crap naming and if we don't correct, the series will get deleted due to the parser not being able to fallback onto folder parsing as the root
                // is the series folder.
                // var existingFolder = seriesDirs.Keys.FirstOrDefault(key => key.Contains(series.OriginalName));
                // if (seriesDirs.Keys.Count == 1 && !string.IsNullOrEmpty(existingFolder))
                // {
                //     seriesDirs = new Dictionary<string, string>();
                //     var path = Directory.GetParent(existingFolder)?.FullName;
                //     if (!libraryPaths.Contains(path) || !libraryPaths.Any(p => p.Contains(path ?? string.Empty)))
                //     {
                //         _logger.LogCritical("[ScanService] Aborted: {SeriesName} has bad naming convention and sits at root of library. Cannot scan series without deletion occuring. Correct file names to have Series Name within it or perform Scan Library", series.OriginalName);
                //         await _eventHub.SendMessageAsync(MessageFactory.Error,
                //             MessageFactory.ErrorEvent($"Scan of {series.Name} aborted", $"{series.OriginalName} has bad naming convention and sits at root of library. Cannot scan series without deletion occuring. Correct file names to have Series Name within it or perform Scan Library"));
                //         return;
                //     }
                //     if (!string.IsNullOrEmpty(path))
                //     {
                //         seriesDirs[path] = string.Empty;
                //     }
                // }

                // Task TrackFiles(IList<ParserInfo> parsedFiles)
                // {
                //     if (parsedFiles.Count == 0) return Task.CompletedTask;
                //     var firstFile = parsedFiles.First();
                //     parsedSeries.Add(new ParsedSeries() {Format = firstFile.Format, Name = firstFile.Series, NormalizedName = Parser.Parser.Normalize(firstFile.Series)}, parsedFiles);
                //     totalFiles += parsedFiles.Count;
                //     return Task.CompletedTask;
                // }
                //
                // var scanElapsedTime2 = await ScanFiles(library, seriesDirs.Keys, false, TrackFiles);
                // _logger.LogInformation("{SeriesName} has bad naming convention, forcing rescan at a higher directory", series.OriginalName);
                //
                // scanElapsedTime += scanElapsedTime2;
                // RemoveParsedInfosNotForSeries(parsedSeries, series);
            }
            // At this point, parsedSeries will have at least one key and we can perform the update. If it still doesn't, just return and don't do anything
            if (parsedSeries.Count == 0) return;
        }


        try
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name));
            var parsedInfos = ParseScannedFiles.GetInfosByName(parsedSeries, series);
            await _processSeries.ProcessSeriesAsync(parsedInfos, allPeople, allTags, allGenres, library);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));

            await CommitAndSend(totalFiles, parsedSeries, sw, scanElapsedTime, series);
            await RemoveAbandonedMetadataKeys();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an error during ScanSeries to update the series");
            await _unitOfWork.RollbackAsync();
        }
        // Tell UI that this series is done
        await _eventHub.SendMessageAsync(MessageFactory.ScanSeries,
            MessageFactory.ScanSeriesEvent(library.Id, seriesId, series.Name));
        await CleanupDbEntities();
        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.TempDirectory));
        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForSeries(library.Id, series.Id, false));
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(library.Id, series.Id, false));
    }

    private async Task<bool> ShouldScanSeries(int seriesId, Library library, IList<string> libraryPaths, Series series)
    {
        var seriesFolderPaths = (await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId))
            .Select(f => _directoryService.FileSystem.FileInfo.FromFileName(f.FilePath).Directory.FullName)
            .Distinct()
            .ToList();

        if (!await CheckMounts(library.Name, seriesFolderPaths))
        {
            _logger.LogCritical(
                "Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return false;
        }

        if (!await CheckMounts(library.Name, libraryPaths))
        {
            _logger.LogCritical(
                "Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return false;
        }

        // If all series Folder paths haven't been modified since last scan, abort
        if (seriesFolderPaths.All(folder => File.GetLastWriteTimeUtc(folder) <= series.LastFolderScanned))
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name));
            _logger.LogInformation(
                "[ScannerService] {SeriesName} scan has no work to do. All folders have not been changed since last scan",
                series.Name);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));
            return false;
        }

        return true;
    }

    private static void RemoveParsedInfosNotForSeries(Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Series series)
    {
        var keys = parsedSeries.Keys;
        foreach (var key in keys.Where(key => !SeriesHelper.FindSeries(series, key))) // series.Format != key.Format ||
        {
            parsedSeries.Remove(key);
        }
    }

    private async Task CommitAndSend(int totalFiles,
        Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Stopwatch sw, long scanElapsedTime, Series series)
    {
        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            _logger.LogInformation(
                "Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {SeriesName}",
                totalFiles, parsedSeries.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, series.Name);
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
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibrary(int libraryId)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        var libraryFolderPaths = library.Folders.Select(fp => fp.Path).ToList();
        if (!await CheckMounts(library.Name, libraryFolderPaths)) return;

        // TODO: Uncomment this
        // If all library Folder paths haven't been modified since last scan, abort
        // if (!library.AnyModificationsSinceLastScan())
        // {
        //     _logger.LogInformation("[ScannerService] {LibraryName} scan has no work to do. All folders have not been changed since last scan", library.Name);
        //     // NOTE: I think we should send this as an Info to the UI, rather than ERROR.
        //     await _eventHub.SendMessageAsync(MessageFactory.Error,
        //         MessageFactory.ErrorEvent($"{library.Name} scan has no work to do",
        //             "All folders have not been changed since last scan. Scan will be aborted."));
        //     return;
        // }

        // Validations are done, now we can start actual scan

        _logger.LogInformation("[ScannerService] Beginning file scan on {LibraryName}", library.Name);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, string.Empty));



        // This doesn't work for something like M:/Manga/ and a series has library folder as root
        var shouldUseLibraryScan = !(await _unitOfWork.LibraryRepository.DoAnySeriesFoldersMatch(libraryFolderPaths));
        if (!shouldUseLibraryScan)
        {
            _logger.LogInformation("Library {LibraryName} consists of one ore more Series folders, using series scan", library.Name);
        }


        var totalFiles = 0;
        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        var seenSeries = new List<ParsedSeries>();

        // The reality is that we need to change how we add and make these thread safe
        var allPeople = new BlockingCollection<Person>();;
        foreach (var person in await _unitOfWork.PersonRepository.GetAllPeople())
        {
            allPeople.Add(person);
        }

        var allGenres = new BlockingCollection<Genre>();
        foreach (var genre in await _unitOfWork.GenreRepository.GetAllGenresAsync())
        {
            allGenres.Add(genre);
        }
        var allTags = new BlockingCollection<Tag>();
        foreach (var tag in await _unitOfWork.TagRepository.GetAllTagsAsync())
        {
            allTags.Add(tag);
        }

        var sw = Stopwatch.StartNew();
        async Task TrackFiles(IList<ParserInfo> parsedFiles)
        {

            if (parsedFiles.Count == 0) return;
            totalFiles += parsedFiles.Count;

            var foundParsedSeries = new ParsedSeries()
            {
                Name = parsedFiles.First().Series,
                NormalizedName = Parser.Parser.Normalize(parsedFiles.First().Series),
                Format = parsedFiles.First().Format
            };
            seenSeries.Add(foundParsedSeries);
            //_processSeries.Enqueue(parsedFiles, library);
            await _processSeries.ProcessSeriesAsync(parsedFiles, allPeople, allTags, allGenres, library);
            parsedSeries.Add(foundParsedSeries, parsedFiles);
        }


        var scanElapsedTime = await ScanFiles(library, libraryFolderPaths, shouldUseLibraryScan, TrackFiles);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(string.Empty, library.Name, ProgressEventType.Ended));


        _logger.LogInformation("[ScannerService] Finished file scan in {ScanAndUpdateTime}. Updating database", scanElapsedTime);

        foreach (var folderPath in library.Folders)
        {
            folderPath.LastScanned = DateTime.UtcNow;
        }

        // TODO: Remove Series not seen from DB



        _unitOfWork.LibraryRepository.Update(library);
        if (await _unitOfWork.CommitAsync())
        {
            _logger.LogInformation(
                "[ScannerService] Finished scan of {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                totalFiles, parsedSeries.Keys.Count, sw.ElapsedMilliseconds, library.Name);
        }
        else
        {
            _logger.LogCritical(
                "[ScannerService] There was a critical error that resulted in a failed scan. Please check logs and rescan");
        }

        await CleanupDbEntities();

        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForLibrary(libraryId, false));
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanLibrary(libraryId, false));
        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.TempDirectory));
    }

    private async Task<long> ScanFiles(Library library, IEnumerable<string> dirs,
        bool isLibraryScan, Func<IList<ParserInfo>, Task> processSeriesInfos = null)
    {
        var scanner = new ParseScannedFiles(_logger, _directoryService, _readingItemService, _eventHub);
        var scanWatch = Stopwatch.StartNew();

        await scanner.ScanLibrariesForSeries2(library.Type, dirs, library.Name,
            isLibraryScan,  processSeriesInfos);

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

    private async Task RemoveAbandonedMetadataKeys()
    {
        await _unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
    }
}
