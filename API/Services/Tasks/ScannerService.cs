using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Comparators;
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
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    Task ScanLibrary(int libraryId);
    Task ScanLibraries();
    Task ScanSeries(int libraryId, int seriesId, CancellationToken token);
}

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

    public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger,
        IMetadataService metadataService, ICacheService cacheService, IEventHub eventHub,
        IFileService fileService, IDirectoryService directoryService, IReadingItemService readingItemService,
        ICacheHelper cacheHelper, IWordCountAnalyzerService wordCountAnalyzerService)
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
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanSeries(int libraryId, int seriesId, CancellationToken token)
    {
        var sw = new Stopwatch();
        var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new[] {seriesId});
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        var folderPaths = library.Folders.Select(f => f.Path).ToList();

        var seriesFolderPaths = (await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId))
            .Select(f => _directoryService.FileSystem.FileInfo.FromFileName(f.FilePath).Directory.FullName)
            .ToList();

        if (!await CheckMounts(library.Name, seriesFolderPaths))
        {
            _logger.LogCritical("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return;
        }

        if (!await CheckMounts(library.Name, library.Folders.Select(f => f.Path).ToList()))
        {
            _logger.LogCritical("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");
            return;
        }

        var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
        var allGenres = await _unitOfWork.GenreRepository.GetAllGenresAsync();
        var allTags = await _unitOfWork.TagRepository.GetAllTagsAsync();

        var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(seriesFolderPaths, files.Select(f => f.FilePath).ToList());
        if (seriesDirs.Keys.Count == 0)
        {
            _logger.LogDebug("Scan Series has files spread outside a main series folder. Defaulting to library folder");
            seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(folderPaths, files.Select(f => f.FilePath).ToList());
        }

        _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
        var (totalFiles, scanElapsedTime, parsedSeries) = await ScanFiles(library, seriesDirs.Keys);



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
                    _logger.LogCritical(ex, "There was an error during ScanSeries to delete the series");
                    await _unitOfWork.RollbackAsync();
                }

            }
            else
            {
                // We need to do an additional check for an edge case: If the scan ran and the files do not match the existing Series name, then it is very likely,
                // the files have crap naming and if we don't correct, the series will get deleted due to the parser not being able to fallback onto folder parsing as the root
                // is the series folder.
                var existingFolder = seriesDirs.Keys.FirstOrDefault(key => key.Contains(series.OriginalName));
                if (seriesDirs.Keys.Count == 1 && !string.IsNullOrEmpty(existingFolder))
                {
                    seriesDirs = new Dictionary<string, string>();
                    var path = Directory.GetParent(existingFolder)?.FullName;
                    if (!folderPaths.Contains(path) || !folderPaths.Any(p => p.Contains(path ?? string.Empty)))
                    {
                        _logger.LogCritical("[ScanService] Aborted: {SeriesName} has bad naming convention and sits at root of library. Cannot scan series without deletion occuring. Correct file names to have Series Name within it or perform Scan Library", series.OriginalName);
                        await _eventHub.SendMessageAsync(MessageFactory.Error,
                            MessageFactory.ErrorEvent($"Scan of {series.Name} aborted", $"{series.OriginalName} has bad naming convention and sits at root of library. Cannot scan series without deletion occuring. Correct file names to have Series Name within it or perform Scan Library"));
                        return;
                    }
                    if (!string.IsNullOrEmpty(path))
                    {
                        seriesDirs[path] = string.Empty;
                    }
                }

                var (totalFiles2, scanElapsedTime2, parsedSeries2) = await ScanFiles(library, seriesDirs.Keys);
                _logger.LogInformation("{SeriesName} has bad naming convention, forcing rescan at a higher directory", series.OriginalName);
                totalFiles += totalFiles2;
                scanElapsedTime += scanElapsedTime2;
                parsedSeries = parsedSeries2;
                RemoveParsedInfosNotForSeries(parsedSeries, series);
            }
        }

        // At this point, parsedSeries will have at least one key and we can perform the update. If it still doesn't, just return and don't do anything
        if (parsedSeries.Count == 0) return;

        // Merge any series together that might have different ParsedSeries but belong to another group of ParsedSeries
        try
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name));
            await UpdateSeries(series, parsedSeries, allPeople, allTags, allGenres, library);
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
            MessageFactory.ScanSeriesEvent(libraryId, seriesId, series.Name));
        await CleanupDbEntities();
        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
        BackgroundJob.Enqueue(() => _metadataService.RefreshMetadataForSeries(libraryId, series.Id, false));
    }

    private static void RemoveParsedInfosNotForSeries(Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Series series)
    {
        var keys = parsedSeries.Keys;
        foreach (var key in keys.Where(key =>
                      series.Format != key.Format || !SeriesHelper.FindSeries(series, key)))
        {
            parsedSeries.Remove(key);
        }
    }

    private async Task CommitAndSend(int totalFiles,
        Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Stopwatch sw, long scanElapsedTime, Series series)
    {
        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            _logger.LogInformation(
                "Processed {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {SeriesName}",
                totalFiles, parsedSeries.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, series.Name);
        }
    }

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
            // NOTE: Food for thought, move this to throw an exception and let a middleware inform the UI to keep the code clean. (We can throw a custom exception which
            // will always propagate to the UI)
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


    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60 * 4)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibraries()
    {
        _logger.LogInformation("Starting Scan of All Libraries");
        var libraries = await _unitOfWork.LibraryRepository.GetLibrariesAsync();
        foreach (var lib in libraries)
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
        Library library;
        try
        {
            library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders);
        }
        catch (Exception ex)
        {
            // This usually only fails if user is not authenticated.
            _logger.LogError(ex, "[ScannerService] There was an issue fetching Library {LibraryId}", libraryId);
            return;
        }

        if (!await CheckMounts(library.Name, library.Folders.Select(f => f.Path).ToList()))
        {
            _logger.LogCritical("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted");

            return;
        }


        _logger.LogInformation("[ScannerService] Beginning file scan on {LibraryName}", library.Name);

        var (totalFiles, scanElapsedTime, series) = await ScanFiles(library, library.Folders.Select(fp => fp.Path));
        _logger.LogInformation("[ScannerService] Finished file scan. Updating database");

        foreach (var folderPath in library.Folders)
        {
            folderPath.LastScanned = DateTime.Now;
        }
        var sw = Stopwatch.StartNew();

        await UpdateLibrary(library, series);

        library.LastScanned = DateTime.Now;
        _unitOfWork.LibraryRepository.Update(library);
        if (await _unitOfWork.CommitAsync())
        {
            _logger.LogInformation(
                "[ScannerService] Finished scan of {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                totalFiles, series.Keys.Count, sw.ElapsedMilliseconds + scanElapsedTime, library.Name);
        }
        else
        {
            _logger.LogCritical(
                "[ScannerService] There was a critical error that resulted in a failed scan. Please check logs and rescan");
        }

        await CleanupDbEntities();

        BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, false));
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanLibrary(libraryId, false));
    }

    private async Task<Tuple<int, long, Dictionary<ParsedSeries, List<ParserInfo>>>> ScanFiles(Library library, IEnumerable<string> dirs)
    {
        var scanner = new ParseScannedFiles(_logger, _directoryService, _readingItemService, _eventHub);
        var scanWatch = new Stopwatch();
        var parsedSeries = await scanner.ScanLibrariesForSeries(library.Type, dirs, library.Name);
        var totalFiles = parsedSeries.Keys.Sum(key => parsedSeries[key].Count);
        var scanElapsedTime = scanWatch.ElapsedMilliseconds;

        return new Tuple<int, long, Dictionary<ParsedSeries, List<ParserInfo>>>(totalFiles, scanElapsedTime, parsedSeries);
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

    private async Task UpdateLibrary(Library library, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
    {
        if (parsedSeries == null) return;

        // Library contains no Series, so we need to fetch series in groups of ChunkSize
        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;

        var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
        var allGenres = await _unitOfWork.GenreRepository.GetAllGenresAsync();
        var allTags = await _unitOfWork.TagRepository.GetAllTagsAsync();

        // Update existing series
        _logger.LogInformation("[ScannerService] Updating existing series for {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size",
            library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            totalTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            _logger.LogInformation("[ScannerService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);
            var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id, new UserParams()
            {
                PageNumber = chunk,
                PageSize = chunkInfo.ChunkSize
            });

            // First, remove any series that are not in parsedSeries list
            var missingSeries = FindSeriesNotOnDisk(nonLibrarySeries, parsedSeries).ToList();

            foreach (var missing in missingSeries)
            {
                _unitOfWork.SeriesRepository.Remove(missing);
            }

            var cleanedSeries = SeriesHelper.RemoveMissingSeries(nonLibrarySeries, missingSeries, out var removeCount);
            if (removeCount > 0)
            {
                _logger.LogInformation("[ScannerService] Removed {RemoveMissingSeries} series that are no longer on disk:", removeCount);
                foreach (var s in missingSeries)
                {
                    _logger.LogDebug("[ScannerService] Removed {SeriesName} ({Format})", s.Name, s.Format);
                }
            }

            // Now, we only have to deal with series that exist on disk. Let's recalculate the volumes for each series
            var librarySeries = cleanedSeries.ToList();

            foreach (var series in librarySeries)
            {
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name));
                await UpdateSeries(series, parsedSeries, allPeople, allTags, allGenres, library);
            }

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[ScannerService] There was an issue writing to the DB. Chunk {ChunkNumber} did not save to DB", chunk);
                foreach (var series in nonLibrarySeries)
                {
                    _logger.LogCritical("[ScannerService] There may be a constraint issue with {SeriesName}", series.OriginalName);
                }

                await _eventHub.SendMessageAsync(MessageFactory.Error,
                    MessageFactory.ErrorEvent("There was an issue writing to the DB. Chunk {ChunkNumber} did not save to DB",
                        "The following series had constraint issues: " + string.Join(",", nonLibrarySeries.Select(s => s.OriginalName))));

                continue;
            }
            _logger.LogInformation(
                "[ScannerService] Processed {SeriesStart} - {SeriesEnd} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, totalTime, library.Name);

            // Emit any series removed
            foreach (var missing in missingSeries)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved, MessageFactory.SeriesRemovedEvent(missing.Id, missing.Name, library.Id));
            }

            foreach (var series in librarySeries)
            {
                // This is something more like, the series has finished updating in the backend. It may or may not have been modified.
                await _eventHub.SendMessageAsync(MessageFactory.ScanSeries, MessageFactory.ScanSeriesEvent(library.Id, series.Id, series.Name));
            }
        }


        // Add new series that have parsedInfos
        _logger.LogDebug("[ScannerService] Adding new series");
        var newSeries = new List<Series>();
        var allSeries = (await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(library.Id)).ToList();
        _logger.LogDebug("[ScannerService] Fetched {AllSeriesCount} series for comparing new series with. There should be {DeltaToParsedSeries} new series",
            allSeries.Count, parsedSeries.Count - allSeries.Count);
        // TODO: Once a parsedSeries is processed, remove the key to free up some memory
        foreach (var (key, infos) in parsedSeries)
        {
            // Key is normalized already
            Series existingSeries;
            try
            {
                existingSeries = allSeries.SingleOrDefault(s => SeriesHelper.FindSeries(s, key));
            }
            catch (Exception e)
            {
                // NOTE: If I ever want to put Duplicates table, this is where it can go
                _logger.LogCritical(e, "[ScannerService] There are multiple series that map to normalized key {Key}. You can manually delete the entity via UI and rescan to fix it. This will be skipped", key.NormalizedName);
                var duplicateSeries = allSeries.Where(s => SeriesHelper.FindSeries(s, key));
                foreach (var series in duplicateSeries)
                {
                    _logger.LogCritical("[ScannerService] Duplicate Series Found: {Key} maps with {Series}", key.Name, series.OriginalName);

                }

                continue;
            }

            if (existingSeries != null) continue;

            var s = DbFactory.Series(infos[0].Series);
            if (!s.SortNameLocked && !string.IsNullOrEmpty(infos[0].SeriesSort))
            {
                s.SortName = infos[0].SeriesSort;
            }
            if (!s.LocalizedNameLocked && !string.IsNullOrEmpty(infos[0].LocalizedSeries))
            {
                s.LocalizedName = infos[0].LocalizedSeries;
            }
            s.Format = key.Format;
            s.LibraryId = library.Id; // We have to manually set this since we aren't adding the series to the Library's series.
            newSeries.Add(s);
        }


        foreach(var series in newSeries)
        {
            _logger.LogDebug("[ScannerService] Processing series {SeriesName}", series.OriginalName);
            await UpdateSeries(series, parsedSeries, allPeople, allTags, allGenres, library);
            _unitOfWork.SeriesRepository.Attach(series);
            try
            {
                await _unitOfWork.CommitAsync();
                _logger.LogInformation(
                    "[ScannerService] Added {NewSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                    newSeries.Count, stopwatch.ElapsedMilliseconds, library.Name);

                // Inform UI of new series added
                await _eventHub.SendMessageAsync(MessageFactory.SeriesAdded, MessageFactory.SeriesAddedEvent(series.Id, series.Name, library.Id));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[ScannerService] There was a critical exception adding new series entry for {SeriesName} with a duplicate index key: {IndexKey} ",
                    series.Name, $"{series.Name}_{series.NormalizedName}_{series.LocalizedName}_{series.LibraryId}_{series.Format}");
            }
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended));

        _logger.LogInformation(
            "[ScannerService] Added {NewSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
            newSeries.Count, stopwatch.ElapsedMilliseconds, library.Name);
    }

    private async Task UpdateSeries(Series series, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries,
        ICollection<Person> allPeople, ICollection<Tag> allTags, ICollection<Genre> allGenres, Library library)
    {
        try
        {
            _logger.LogInformation("[ScannerService] Processing series {SeriesName}", series.OriginalName);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));

            // Get all associated ParsedInfos to the series. This includes infos that use a different filename that matches Series LocalizedName
            var parsedInfos = ParseScannedFiles.GetInfosByName(parsedSeries, series);
            UpdateVolumes(series, parsedInfos, allPeople, allTags, allGenres);
            series.Pages = series.Volumes.Sum(v => v.Pages);

            series.NormalizedName = Parser.Parser.Normalize(series.Name);
            series.Metadata ??= DbFactory.SeriesMetadata(new List<CollectionTag>());
            if (series.Format == MangaFormat.Unknown)
            {
                series.Format = parsedInfos[0].Format;
            }
            series.OriginalName ??= parsedInfos[0].Series;
            if (string.IsNullOrEmpty(series.SortName))
            {
                series.SortName = series.Name;
            }
            if (!series.SortNameLocked)
            {
                series.SortName = series.Name;
                if (!string.IsNullOrEmpty(parsedInfos[0].SeriesSort))
                {
                    series.SortName = parsedInfos[0].SeriesSort;
                }
            }

            // parsedInfos[0] is not the first volume or chapter. We need to find it
            var localizedSeries = parsedInfos.Select(p => p.LocalizedSeries).FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (!series.LocalizedNameLocked && !string.IsNullOrEmpty(localizedSeries))
            {
                series.LocalizedName = localizedSeries;
            }

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));

            UpdateSeriesMetadata(series, allPeople, allGenres, allTags, library.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an exception updating volumes for {SeriesName}", series.Name);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));
    }

    public static IEnumerable<Series> FindSeriesNotOnDisk(IEnumerable<Series> existingSeries, Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries)
    {
        return existingSeries.Where(es => !ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(es, parsedSeries));
    }

    private async Task RemoveAbandonedMetadataKeys()
    {
        await _unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
    }


    private static void UpdateSeriesMetadata(Series series, ICollection<Person> allPeople, ICollection<Genre> allGenres, ICollection<Tag> allTags, LibraryType libraryType)
    {
        var isBook = libraryType == LibraryType.Book;
        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, isBook);

        var firstFile = firstChapter?.Files.FirstOrDefault();
        if (firstFile == null) return;
        if (Parser.Parser.IsPdf(firstFile.FilePath)) return;

        var chapters = series.Volumes.SelectMany(volume => volume.Chapters).ToList();

        // Update Metadata based on Chapter metadata
        series.Metadata.ReleaseYear = chapters.Min(c => c.ReleaseDate.Year);

        if (series.Metadata.ReleaseYear < 1000)
        {
            // Not a valid year, default to 0
            series.Metadata.ReleaseYear = 0;
        }

        // Set the AgeRating as highest in all the comicInfos
        if (!series.Metadata.AgeRatingLocked) series.Metadata.AgeRating = chapters.Max(chapter => chapter.AgeRating);

        series.Metadata.TotalCount = chapters.Max(chapter => chapter.TotalCount);
        series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);
        // To not have to rely completely on ComicInfo, try to parse out if the series is complete by checking parsed filenames as well.
        if (series.Metadata.MaxCount != series.Metadata.TotalCount)
        {
            var maxVolume = series.Volumes.Max(v => (int) Parser.Parser.MaxNumberFromRange(v.Name));
            var maxChapter = chapters.Max(c => (int) Parser.Parser.MaxNumberFromRange(c.Range));
            if (maxVolume == series.Metadata.TotalCount) series.Metadata.MaxCount = maxVolume;
            else if (maxChapter == series.Metadata.TotalCount) series.Metadata.MaxCount = maxChapter;
        }


        if (!series.Metadata.PublicationStatusLocked)
        {
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
            if (series.Metadata.MaxCount >= series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
            {
                series.Metadata.PublicationStatus = PublicationStatus.Completed;
            } else if (series.Metadata.TotalCount > 0 && series.Metadata.MaxCount > 0)
            {
                series.Metadata.PublicationStatus = PublicationStatus.Ended;
            }
        }

        if (!string.IsNullOrEmpty(firstChapter.Summary) && !series.Metadata.SummaryLocked)
        {
            series.Metadata.Summary = firstChapter.Summary;
        }

        if (!string.IsNullOrEmpty(firstChapter.Language) && !series.Metadata.LanguageLocked)
        {
            series.Metadata.Language = firstChapter.Language;
        }


        void HandleAddPerson(Person person)
        {
            PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
            allPeople.Add(person);
        }

        // Handle People
        foreach (var chapter in chapters)
        {
            if (!series.Metadata.WriterLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Writer).Select(p => p.Name), PersonRole.Writer,
                    HandleAddPerson);
            }

            if (!series.Metadata.CoverArtistLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.CoverArtist).Select(p => p.Name), PersonRole.CoverArtist,
                    HandleAddPerson);
            }

            if (!series.Metadata.PublisherLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Publisher).Select(p => p.Name), PersonRole.Publisher,
                    HandleAddPerson);
            }

            if (!series.Metadata.CharacterLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Character).Select(p => p.Name), PersonRole.Character,
                    HandleAddPerson);
            }

            if (!series.Metadata.ColoristLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Colorist).Select(p => p.Name), PersonRole.Colorist,
                    HandleAddPerson);
            }

            if (!series.Metadata.EditorLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Editor).Select(p => p.Name), PersonRole.Editor,
                    HandleAddPerson);
            }

            if (!series.Metadata.InkerLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Inker).Select(p => p.Name), PersonRole.Inker,
                    HandleAddPerson);
            }

            if (!series.Metadata.LettererLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Letterer).Select(p => p.Name), PersonRole.Letterer,
                    HandleAddPerson);
            }

            if (!series.Metadata.PencillerLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Penciller).Select(p => p.Name), PersonRole.Penciller,
                    HandleAddPerson);
            }

            if (!series.Metadata.TranslatorLocked)
            {
                PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Translator).Select(p => p.Name), PersonRole.Translator,
                    HandleAddPerson);
            }

            if (!series.Metadata.TagsLocked)
            {
                TagHelper.UpdateTag(allTags, chapter.Tags.Select(t => t.Title), false, (tag, _) =>
                {
                    TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag);
                    allTags.Add(tag);
                });
            }

            if (!series.Metadata.GenresLocked)
            {
                GenreHelper.UpdateGenre(allGenres, chapter.Genres.Select(t => t.Title), false, genre =>
                {
                    GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre);
                    allGenres.Add(genre);
                });
            }
        }

        // NOTE: The issue here is that people is just from chapter, but series metadata might already have some people on it
        // I might be able to filter out people that are in locked fields?
        var people = chapters.SelectMany(c => c.People).ToList();
        PersonHelper.KeepOnlySamePeopleBetweenLists(series.Metadata.People,
            people, person =>
            {
                switch (person.Role)
                {
                    case PersonRole.Writer:
                        if (!series.Metadata.WriterLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Penciller:
                        if (!series.Metadata.PencillerLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Inker:
                        if (!series.Metadata.InkerLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Colorist:
                        if (!series.Metadata.ColoristLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Letterer:
                        if (!series.Metadata.LettererLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.CoverArtist:
                        if (!series.Metadata.CoverArtistLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Editor:
                        if (!series.Metadata.EditorLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Publisher:
                        if (!series.Metadata.PublisherLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Character:
                        if (!series.Metadata.CharacterLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Translator:
                        if (!series.Metadata.TranslatorLocked) series.Metadata.People.Remove(person);
                        break;
                    case PersonRole.Other:
                    default:
                        series.Metadata.People.Remove(person);
                        break;
                }
            });
    }



    private void UpdateVolumes(Series series, IList<ParserInfo> parsedInfos, ICollection<Person> allPeople, ICollection<Tag> allTags, ICollection<Genre> allGenres)
    {
        var startingVolumeCount = series.Volumes.Count;
        // Add new volumes and update chapters per volume
        var distinctVolumes = parsedInfos.DistinctVolumes();
        _logger.LogDebug("[ScannerService] Updating {DistinctVolumes} volumes on {SeriesName}", distinctVolumes.Count, series.Name);
        foreach (var volumeNumber in distinctVolumes)
        {
            var volume = series.Volumes.SingleOrDefault(s => s.Name == volumeNumber);
            if (volume == null)
            {
                volume = DbFactory.Volume(volumeNumber);
                series.Volumes.Add(volume);
                _unitOfWork.VolumeRepository.Add(volume);
            }

            _logger.LogDebug("[ScannerService] Parsing {SeriesName} - Volume {VolumeNumber}", series.Name, volume.Name);
            var infos = parsedInfos.Where(p => p.Volumes == volumeNumber).ToArray();
            UpdateChapters(series, volume, infos);
            volume.Pages = volume.Chapters.Sum(c => c.Pages);

            // Update all the metadata on the Chapters
            foreach (var chapter in volume.Chapters)
            {
                var firstFile = chapter.Files.MinBy(x => x.Chapter);
                if (firstFile == null || _cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, false, firstFile)) continue;
                try
                {
                    var firstChapterInfo = infos.SingleOrDefault(i => i.FullFilePath.Equals(firstFile.FilePath));
                    UpdateChapterFromComicInfo(chapter, allPeople, allTags, allGenres, firstChapterInfo?.ComicInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was some issue when updating chapter's metadata");
                }
            }
        }

        // Remove existing volumes that aren't in parsedInfos
        var nonDeletedVolumes = series.Volumes.Where(v => parsedInfos.Select(p => p.Volumes).Contains(v.Name)).ToList();
        if (series.Volumes.Count != nonDeletedVolumes.Count)
        {
            _logger.LogDebug("[ScannerService] Removed {Count} volumes from {SeriesName} where parsed infos were not mapping with volume name",
                (series.Volumes.Count - nonDeletedVolumes.Count), series.Name);
            var deletedVolumes = series.Volumes.Except(nonDeletedVolumes);
            foreach (var volume in deletedVolumes)
            {
                var file = volume.Chapters.FirstOrDefault()?.Files?.FirstOrDefault()?.FilePath ?? "";
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    _logger.LogError(
                        "[ScannerService] Volume cleanup code was trying to remove a volume with a file still existing on disk. File: {File}",
                        file);
                }

                _logger.LogDebug("[ScannerService] Removed {SeriesName} - Volume {Volume}: {File}", series.Name, volume.Name, file);
            }

            series.Volumes = nonDeletedVolumes;
        }

        _logger.LogDebug("[ScannerService] Updated {SeriesName} volumes from {StartingVolumeCount} to {VolumeCount}",
            series.Name, startingVolumeCount, series.Volumes.Count);
    }

    private void UpdateChapters(Series series, Volume volume, IList<ParserInfo> parsedInfos)
    {
        // Add new chapters
        foreach (var info in parsedInfos)
        {
            // Specials go into their own chapters with Range being their filename and IsSpecial = True. Non-Specials with Vol and Chap as 0
            // also are treated like specials for UI grouping.
            Chapter chapter;
            try
            {
                chapter = volume.Chapters.GetChapterByRange(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{FileName} mapped as '{Series} - Vol {Volume} Ch {Chapter}' is a duplicate, skipping", info.FullFilePath, info.Series, info.Volumes, info.Chapters);
                continue;
            }

            if (chapter == null)
            {
                _logger.LogDebug(
                    "[ScannerService] Adding new chapter, {Series} - Vol {Volume} Ch {Chapter}", info.Series, info.Volumes, info.Chapters);
                chapter = DbFactory.Chapter(info);
                volume.Chapters.Add(chapter);
                series.LastChapterAdded = DateTime.Now;
            }
            else
            {
                chapter.UpdateFrom(info);
            }

            if (chapter == null) continue;
            // Add files
            var specialTreatment = info.IsSpecialInfo();
            AddOrUpdateFileForChapter(chapter, info);
            chapter.Number = Parser.Parser.MinNumberFromRange(info.Chapters) + string.Empty;
            chapter.Range = specialTreatment ? info.Filename : info.Chapters;
        }


        // Remove chapters that aren't in parsedInfos or have no files linked
        var existingChapters = volume.Chapters.ToList();
        foreach (var existingChapter in existingChapters)
        {
            if (existingChapter.Files.Count == 0 || !parsedInfos.HasInfo(existingChapter))
            {
                _logger.LogDebug("[ScannerService] Removed chapter {Chapter} for Volume {VolumeNumber} on {SeriesName}", existingChapter.Range, volume.Name, parsedInfos[0].Series);
                volume.Chapters.Remove(existingChapter);
            }
            else
            {
                // Ensure we remove any files that no longer exist AND order
                existingChapter.Files = existingChapter.Files
                    .Where(f => parsedInfos.Any(p => p.FullFilePath == f.FilePath))
                    .OrderByNatural(f => f.FilePath).ToList();
                existingChapter.Pages = existingChapter.Files.Sum(f => f.Pages);
            }
        }
    }

    private void AddOrUpdateFileForChapter(Chapter chapter, ParserInfo info)
    {
        chapter.Files ??= new List<MangaFile>();
        var existingFile = chapter.Files.SingleOrDefault(f => f.FilePath == info.FullFilePath);
        if (existingFile != null)
        {
            existingFile.Format = info.Format;
            if (!_fileService.HasFileBeenModifiedSince(existingFile.FilePath, existingFile.LastModified) && existingFile.Pages != 0) return;
            existingFile.Pages = _readingItemService.GetNumberOfPages(info.FullFilePath, info.Format);
            // We skip updating DB here with last modified time so that metadata refresh can do it
        }
        else
        {
            var file = DbFactory.MangaFile(info.FullFilePath, info.Format, _readingItemService.GetNumberOfPages(info.FullFilePath, info.Format));
            if (file == null) return;

            chapter.Files.Add(file);
        }
    }

    private void UpdateChapterFromComicInfo(Chapter chapter, ICollection<Person> allPeople, ICollection<Tag> allTags, ICollection<Genre> allGenres, ComicInfo? info)
    {
        var firstFile = chapter.Files.MinBy(x => x.Chapter);
        if (firstFile == null ||
            _cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, false, firstFile)) return;

        var comicInfo = info;
        if (info == null)
        {
            comicInfo = _readingItemService.GetComicInfo(firstFile.FilePath);
        }

        if (comicInfo == null) return;
        _logger.LogDebug("[ScannerService] Read ComicInfo for {File}", firstFile.FilePath);

        chapter.AgeRating = ComicInfo.ConvertAgeRatingToEnum(comicInfo.AgeRating);

        if (!string.IsNullOrEmpty(comicInfo.Title))
        {
            chapter.TitleName = comicInfo.Title.Trim();
        }

        if (!string.IsNullOrEmpty(comicInfo.Summary))
        {
            chapter.Summary = comicInfo.Summary;
        }

        if (!string.IsNullOrEmpty(comicInfo.LanguageISO))
        {
            chapter.Language = comicInfo.LanguageISO;
        }

        if (comicInfo.Count > 0)
        {
            chapter.TotalCount = comicInfo.Count;
        }

        // This needs to check against both Number and Volume to calculate Count
        if (!string.IsNullOrEmpty(comicInfo.Number) && float.Parse(comicInfo.Number) > 0)
        {
            chapter.Count = (int) Math.Floor(float.Parse(comicInfo.Number));
        }
        if (!string.IsNullOrEmpty(comicInfo.Volume) && float.Parse(comicInfo.Volume) > 0)
        {
            chapter.Count = Math.Max(chapter.Count, (int) Math.Floor(float.Parse(comicInfo.Volume)));
        }





        if (comicInfo.Year > 0)
        {
            var day = Math.Max(comicInfo.Day, 1);
            var month = Math.Max(comicInfo.Month, 1);
            chapter.ReleaseDate = DateTime.Parse($"{month}/{day}/{comicInfo.Year}");
        }

        if (!string.IsNullOrEmpty(comicInfo.Colorist))
        {
            var people = comicInfo.Colorist.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Colorist);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Colorist,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Characters))
        {
            var people = comicInfo.Characters.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Character);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Character,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Translator))
        {
            var people = comicInfo.Translator.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Translator);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Translator,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Tags))
        {
            var tags = comicInfo.Tags.Split(",").Select(s => s.Trim()).ToList();
            // Remove all tags that aren't matching between chapter tags and metadata
            TagHelper.KeepOnlySameTagBetweenLists(chapter.Tags, tags.Select(t => DbFactory.Tag(t, false)).ToList());
            TagHelper.UpdateTag(allTags, tags, false,
                (tag, _) =>
                {
                    chapter.Tags.Add(tag);
                });
        }

        if (!string.IsNullOrEmpty(comicInfo.Writer))
        {
            var people = comicInfo.Writer.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Writer);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Writer,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Editor))
        {
            var people = comicInfo.Editor.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Editor);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Editor,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Inker))
        {
            var people = comicInfo.Inker.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Inker);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Inker,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Letterer))
        {
            var people = comicInfo.Letterer.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Letterer);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Letterer,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Penciller))
        {
            var people = comicInfo.Penciller.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Penciller);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Penciller,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.CoverArtist))
        {
            var people = comicInfo.CoverArtist.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.CoverArtist);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.CoverArtist,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Publisher))
        {
            var people = comicInfo.Publisher.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Publisher);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Publisher,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Genre))
        {
            var genres = comicInfo.Genre.Split(",");
            GenreHelper.KeepOnlySameGenreBetweenLists(chapter.Genres, genres.Select(g => DbFactory.Genre(g, false)).ToList());
            GenreHelper.UpdateGenre(allGenres, genres, false,
                genre => chapter.Genres.Add(genre));
        }
    }
}
