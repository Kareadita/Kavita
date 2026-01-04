using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Settings;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
#nullable enable

public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    /// <param name="forceUpdate">Don't perform optimization checks, defaults to false</param>
    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibrary(int libraryId, bool forceUpdate = false, bool isSingleScan = true);

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibraries(bool forceUpdate = false);

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true);

    Task ScanFolder(string folder, string originalPath, bool abortOnNoSeriesMatch = false);
    Task AnalyzeFiles();

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
    public const string Name = "ScannerService";
    private const int Timeout = 60 * 60 * 60; // 2.5 days
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ScannerService> _logger;
    private readonly IMetadataService _metadataService;
    private readonly ICacheService _cacheService;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScannerService(IUnitOfWork unitOfWork, ILogger<ScannerService> logger,
        IMetadataService metadataService, ICacheService cacheService, IEventHub eventHub,
        IDirectoryService directoryService, IReadingItemService readingItemService,
        IServiceScopeFactory scopeFactory, IWordCountAnalyzerService wordCountAnalyzerService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _metadataService = metadataService;
        _cacheService = cacheService;
        _eventHub = eventHub;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _scopeFactory = scopeFactory;
        _wordCountAnalyzerService = wordCountAnalyzerService;
    }

    /// <summary>
    /// This is only used for v0.7 to get files analyzed
    /// </summary>
    public async Task AnalyzeFiles()
    {
        _logger.LogInformation("Starting Analyze Files task");
        var missingExtensions = await _unitOfWork.MangaFileRepository.GetAllWithMissingExtension();
        if (missingExtensions.Count == 0)
        {
            _logger.LogInformation("Nothing to do");
            return;
        }

        var sw = Stopwatch.StartNew();

        foreach (var file in missingExtensions)
        {
            var fileInfo = _directoryService.FileSystem.FileInfo.New(file.FilePath);
            if (!fileInfo.Exists)continue;
            file.Extension = fileInfo.Extension.ToLowerInvariant();
            file.Bytes = fileInfo.Length;
            _unitOfWork.MangaFileRepository.Update(file);
        }

        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Completed Analyze Files task in {ElapsedTime}", sw.Elapsed);
    }

    /// <summary>
    /// Given a generic folder path, will invoke a Series scan or Library scan.
    /// </summary>
    /// <remarks>This will Schedule the job to run 1 minute in the future to allow for any close-by duplicate requests to be dropped</remarks>
    /// <param name="folder">Normalized folder</param>
    /// <param name="originalPath">If invoked from LibraryWatcher, this maybe a nested folder and can allow for optimization</param>
    /// <param name="abortOnNoSeriesMatch"></param>
    public async Task ScanFolder(string folder, string originalPath, bool abortOnNoSeriesMatch = false)
    {
        Series? series = null;
        try
        {
            series = await _unitOfWork.SeriesRepository.GetSeriesThatContainsLowestFolderPath(originalPath,
                         SeriesIncludes.Library) ??
                     await _unitOfWork.SeriesRepository.GetSeriesByFolderPath(originalPath, SeriesIncludes.Library) ??
                     await _unitOfWork.SeriesRepository.GetSeriesByFolderPath(folder, SeriesIncludes.Library);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Equals("Sequence contains more than one element."))
            {
                _logger.LogCritical(ex, "[ScannerService] Multiple series map to this folder or folder is at library root. Library scan will be used for ScanFolder");
            }
        }

        if (series != null)
        {
            if (TaskScheduler.HasScanTaskRunningForSeries(series.Id))
            {
                _logger.LogTrace("[ScannerService] Scan folder invoked for {Folder} but a task is already queued for this series. Dropping request", folder);
                return;
            }

            _logger.LogInformation("[ScannerService] Scan folder invoked for {Folder}, Series matched to folder and ScanSeries enqueued for 1 minute", folder);
            BackgroundJob.Schedule(() => ScanSeries(series.Id, true), TimeSpan.FromMinutes(1));
            return;
        }

        if (abortOnNoSeriesMatch) return;


        // This is basically rework of what's already done in Library Watcher but is needed if invoked via API
        var parentDirectory = _directoryService.GetParentDirectoryName(folder);
        if (string.IsNullOrEmpty(parentDirectory)) return;

        var libraries = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync()).ToList();
        var libraryFolders = libraries.SelectMany(l => l.Folders);
        var libraryFolder = libraryFolders.Select(Parser.NormalizePath).FirstOrDefault(f => f.Contains(parentDirectory));

        if (string.IsNullOrEmpty(libraryFolder)) return;
        var library = libraries.Find(l => l.Folders.Select(Parser.NormalizePath).Contains(libraryFolder));

        if (library != null)
        {
            if (TaskScheduler.HasScanTaskRunningForLibrary(library.Id))
            {
                _logger.LogTrace("[ScannerService] Scan folder invoked for {Folder} but a task is already queued for this library. Dropping request", folder);
                return;
            }
            BackgroundJob.Schedule(() => ScanLibrary(library.Id, false, true), TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>
    /// Scans just an existing Series for changes. If the series doesn't exist, will delete it.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="bypassFolderOptimizationChecks">Not Used. Scan series will always force</param>
    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(Timeout)]
    [AutomaticRetry(Attempts = 200, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true)
    {
        if (TaskScheduler.HasAlreadyEnqueuedTask(Name, "ScanSeries", [seriesId, bypassFolderOptimizationChecks], TaskScheduler.ScanQueue))
        {
            _logger.LogInformation("[ScannerService] Scan series invoked but a task is already running/enqueued. Dropping request");
            return;
        }

        var sw = Stopwatch.StartNew();

        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        if (series == null) return; // This can occur when UI deletes a series but doesn't update and user re-requests update

        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var existingChapterIdsToClean = await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new[] {seriesId});

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId, LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        if (library == null) return;

        var libraryPaths = library.Folders.Select(f => f.Path).ToList();
        if (await ShouldScanSeries(seriesId, library, libraryPaths, series, true) != ScanCancelReason.NoCancel)
        {
            BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForSeries(serverSettings, series.LibraryId, seriesId, false, false));
            BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(library.Id, seriesId, bypassFolderOptimizationChecks));
            return;
        }

        // TODO: We need to refactor this to handle the path changes better
        var folderPath = series.LowestFolderPath ?? series.FolderPath;
        if (string.IsNullOrEmpty(folderPath) || !_directoryService.Exists(folderPath))
        {
            // We don't care if it's multiple due to new scan loop enforcing all in one root directory
            var files = await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId);
            var seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths,
                files.Select(f => f.FilePath).ToList());
            if (seriesDirs.Keys.Count == 0)
            {
                _logger.LogCritical("Scan Series has files spread outside a main series folder. Defaulting to library folder (this is expensive)");
                await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.InfoEvent($"{series.Name} is not organized well and scan series will be expensive!", "Scan Series has files spread outside a main series folder. Defaulting to library folder (this is expensive)"));
                seriesDirs = _directoryService.FindHighestDirectoriesFromFiles(libraryPaths, files.Select(f => f.FilePath).ToList());
            }

            folderPath = seriesDirs.Keys.FirstOrDefault();

            // We should check if folderPath is a library folder path and if so, return early and tell user to correct their setup.
            if (!string.IsNullOrEmpty(folderPath) && libraryPaths.Contains(folderPath))
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

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Started, series.Name, 1));

        _logger.LogInformation("Beginning file scan on {SeriesName}", series.Name);
        var (scanElapsedTime, parsedSeries) = await ScanFiles(library, [folderPath],
            false, true);

        _logger.LogInformation("ScanFiles for {Series} took {Time} milliseconds", series.Name, scanElapsedTime);

        // Remove any parsedSeries keys that don't belong to our series. This can occur when users store 2 series in the same folder
        RemoveParsedInfosNotForSeries(parsedSeries, series);

        // If nothing was found, first validate any of the files still exist. If they don't then we have a deletion and can skip the rest of the logic flow
        if (parsedSeries.Count == 0)
        {
             var seriesFiles = (await _unitOfWork.SeriesRepository.GetFilesForSeries(series.Id));
             if (!string.IsNullOrEmpty(series.FolderPath) &&
                 !seriesFiles.Where(f => f.FilePath.Contains(series.FolderPath)).Any(m => File.Exists(m.FilePath)))
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
        }

        // At this point, parsedSeries will have at least one key then we can perform the update. If it still doesn't, just return and don't do anything
        // Don't allow any processing on files that aren't part of this series
        var toProcess = parsedSeries.Keys.Where(key =>
            key.NormalizedName.Equals(series.NormalizedName) ||
            key.NormalizedName.Equals(series.OriginalName?.ToNormalized()))
            .ToList();

        var toProcessList = toProcess.Select(k => parsedSeries[k]).ToList();
        var totalCount = toProcessList.Count;
        var current = 0;

        foreach (var pSeries in toProcessList)
        {
            current++;

            using var scope = _scopeFactory.CreateScope();
            var processSeries = scope.ServiceProvider.GetRequiredService<IProcessSeries>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var scopedLibrary = (await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns))!;

            var processedSeriesId = await processSeries.ProcessSeriesAsync(settings, pSeries, new ProcessSeriesArgs
            {
                Library = scopedLibrary,
                LeftToProcess = totalCount - current,
                TotalToProcess = totalCount,
                ForceUpdate = bypassFolderOptimizationChecks,
            });

            if (processedSeriesId != null)
            {
                var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                var wordCountAnalyzerService = scope.ServiceProvider.GetRequiredService<IWordCountAnalyzerService>();

                await metadataService.GenerateCoversForSeries(serverSettings, scopedLibrary.Id, processedSeriesId.Value, bypassFolderOptimizationChecks, false);
                await wordCountAnalyzerService.ScanSeries(scopedLibrary.Id, processedSeriesId.Value, bypassFolderOptimizationChecks);
            }
        }

        // Tell UI that this series is done
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, series.Name));

        await _metadataService.RemoveAbandonedMetadataKeys();

        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(existingChapterIdsToClean));
        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.CacheDirectory));
    }

    private static Dictionary<ParsedSeries, IList<ParserInfo>> TrackFoundSeriesAndFiles(IList<ScannedSeriesResult> seenSeries)
    {
        // Why does this only grab things that have changed?
        var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        foreach (var series in seenSeries.Where(s => s.ParsedInfos.Count > 0)) // && s.HasChanged
        {
            var parsedFiles = series.ParsedInfos;
            series.ParsedSeries.HasChanged = series.HasChanged;

            if (series.HasChanged)
            {
                parsedSeries.Add(series.ParsedSeries, parsedFiles);
            }
            else
            {
                parsedSeries.Add(series.ParsedSeries, []);
            }
        }

        return parsedSeries;
    }

    private async Task<ScanCancelReason> ShouldScanSeries(int seriesId, Library library, IList<string> libraryPaths, Series series, bool bypassFolderChecks = false)
    {
        var seriesFolderPaths = (await _unitOfWork.SeriesRepository.GetFilesForSeries(seriesId))
            .Select(f => _directoryService.FileSystem.FileInfo.New(f.FilePath).Directory?.FullName ?? string.Empty)
            .Where(f => !string.IsNullOrEmpty(f))
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

        // If all series Folder paths haven't been modified since last scan, abort (NOTE: This flow never happens as ScanSeries will always bypass)
        if (!bypassFolderChecks)
        {

            var allFolders = seriesFolderPaths.SelectMany(path => _directoryService.GetDirectories(path)).ToList();
            allFolders.AddRange(seriesFolderPaths);

            try
            {
                if (allFolders.TrueForAll(folder => _directoryService.GetLastWriteTime(folder) <= series.LastFolderScanned))
                {
                    _logger.LogInformation(
                        "[ScannerService] {SeriesName} scan has no work to do. All folders have not been changed since last scan",
                        series.Name);
                    await _eventHub.SendMessageAsync(MessageFactory.Info,
                        MessageFactory.InfoEvent($"{series.Name} scan has no work to do",
                            $"All folders have not been changed since last scan ({series.LastFolderScanned.ToString(CultureInfo.CurrentCulture)}). Scan will be aborted."));
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
                        "The folder the series was in is missing. Delete series manually or perform a library scan."));
                return ScanCancelReason.NoCancel;
            }
        }


        return ScanCancelReason.NoCancel;
    }

    private static void RemoveParsedInfosNotForSeries(Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Series series)
    {
        var keys = parsedSeries.Keys;
        foreach (var key in keys.Where(key => !SeriesHelper.FindSeries(series, key)))
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
            _logger.LogCritical("[ScannerService] Some of the root folders for library ({LibraryName} are not accessible. Please check that drives are connected and rescan. Scan will be aborted", libraryName);

            await _eventHub.SendMessageAsync(MessageFactory.Error,
                MessageFactory.ErrorEvent("Some of the root folders for library are not accessible. Please check that drives are connected and rescan. Scan will be aborted",
                    string.Join(", ", folders.Where(f => !_directoryService.IsDriveMounted(f)))));

            return false;
        }


        // For Docker instances check if any of the folder roots are not available (ie disconnected volumes, etc) and fail if any of them are
        if (folders.Any(f => _directoryService.IsDirectoryEmpty(f)))
        {
            // That way logging and UI informing is all in one place with full context
            _logger.LogError("[ScannerService] Some of the root folders for the library are empty. " +
                             "Either your mount has been disconnected or you are trying to delete all series in the library. " +
                             "Scan has been aborted. " +
                             "Check that your mount is connected or change the library's root folder and rescan");

            await _eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.ErrorEvent( $"Some of the root folders for the library, {libraryName}, are empty.",
                "Either your mount has been disconnected or you are trying to delete all series in the library. " +
                "Scan has been aborted. " +
                "Check that your mount is connected or change the library's root folder and rescan"));

            return false;
        }

        return true;
    }

    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(Timeout)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibraries(bool forceUpdate = false)
    {
        _logger.LogInformation("[ScannerService] Starting Scan of All Libraries, Forced: {Forced}", forceUpdate);
        foreach (var lib in await _unitOfWork.LibraryRepository.GetLibrariesAsync())
        {
            // BUG: This will trigger the first N libraries to scan over and over if there is always an interruption later in the chain
            if (TaskScheduler.HasScanTaskRunningForLibrary(lib.Id))
            {
                // We don't need to send SignalR event as this is a background job that user doesn't need insight into
                _logger.LogInformation("[ScannerService] Scan library invoked via nightly scan job but a task is already running for {LibraryName}. Rescheduling for 4 hours", lib.Name);
                await Task.Delay(TimeSpan.FromHours(4));
            }

            await ScanLibrary(lib.Id, forceUpdate, true);
        }

        _logger.LogInformation("[ScannerService] Scan of All Libraries Finished");
    }


    /// <summary>
    /// Scans a library for file changes.
    /// Will kick off a scheduled background task to refresh metadata,
    /// ie) all entities will be rechecked for new cover images and comicInfo.xml changes
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate">Defaults to false</param>
    /// <param name="isSingleScan">Defaults to true. Is this a standalone invocation or is it in a loop?</param>
    [Queue(TaskScheduler.ScanQueue)]
    [DisableConcurrentExecution(Timeout)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibrary(int libraryId, bool forceUpdate = false, bool isSingleScan = true)
    {
        var sw = Stopwatch.StartNew();
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);

        var libraryFolderPaths = library!.Folders.Select(fp => fp.Path).ToList();
        if (!await CheckMounts(library.Name, libraryFolderPaths)) return;


        // Validations are done, now we can start actual scan
        _logger.LogInformation("[ScannerService] Beginning file scan on {LibraryName}", library.Name);

        if (!library.EnableMetadata)
        {
            _logger.LogInformation("[ScannerService] Warning! {LibraryName} has metadata turned off", library.Name);
        }

        // This doesn't work for something like M:/Manga/ and a series has library folder as root
        var shouldUseLibraryScan = !(await _unitOfWork.LibraryRepository.DoAnySeriesFoldersMatch(libraryFolderPaths));
        if (!shouldUseLibraryScan)
        {
            _logger.LogError("[ScannerService] Library {LibraryName} consists of one or more Series folders as a library root, using series scan", library.Name);
        }


        _logger.LogDebug("[ScannerService] Library {LibraryName} Step 1: Scan & Parse Files", library.Name);
        var (scanElapsedTime, parsedSeries) = await ScanFiles(library, libraryFolderPaths,
            shouldUseLibraryScan, forceUpdate);

        // We need to remove any keys where there is no actual parser info
        _logger.LogDebug("[ScannerService] Library {LibraryName} Step 2: Process and Update Database", library.Name);
        var totalFiles = await ProcessParsedSeries(forceUpdate, parsedSeries, library, scanElapsedTime);

        UpdateLastScanned(library);
        _unitOfWork.LibraryRepository.Update(library);

        _logger.LogDebug("[ScannerService] Library {LibraryName} Step 3: Save Library", library.Name);
        if (await _unitOfWork.CommitAsync())
        {
            if (totalFiles == 0)
            {
                _logger.LogInformation(
                    "[ScannerService] Finished library scan of {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}. There were no changes",
                    parsedSeries.Count, sw.ElapsedMilliseconds, library.Name);
            }
            else
            {
                _logger.LogInformation(
                    "[ScannerService] Finished library scan of {TotalFiles} files and {ParsedSeriesCount} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                    totalFiles, parsedSeries.Count, sw.ElapsedMilliseconds, library.Name);
            }

            _logger.LogDebug("[ScannerService] Library {LibraryName} Step 5: Remove Deleted Series", library.Name);
            await RemoveSeriesNotFound(parsedSeries, library);
        }
        else
        {
            _logger.LogCritical(
                "[ScannerService] There was a critical error that resulted in a failed scan. Please check logs and rescan");
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(library.Name, ProgressEventType.Ended, string.Empty));
        await _metadataService.RemoveAbandonedMetadataKeys();

        BackgroundJob.Enqueue(() => _directoryService.ClearDirectory(_directoryService.CacheDirectory));
    }

    private async Task RemoveSeriesNotFound(Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Library library)
    {
        try
        {
            _logger.LogDebug("[ScannerService] Removing series that were not found during the scan");

            var removedSeries = await _unitOfWork.SeriesRepository.RemoveSeriesNotInList(parsedSeries.Keys.ToList(), library.Id);
            _logger.LogDebug("[ScannerService] Found {Count} series to remove: {SeriesList}",
                removedSeries.Count, string.Join(", ", removedSeries.Select(s => s.Name)));

            // Commit the changes
            await _unitOfWork.CommitAsync();

            // Notify for each removed series
            foreach (var series in removedSeries)
            {
                await _eventHub.SendMessageAsync(
                    MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(series.Id, series.Name, series.LibraryId),
                    false
                );
            }

            _logger.LogDebug("[ScannerService] Series removal process completed");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[ScannerService] Error during series cleanup. Please check logs and rescan");
        }
    }

    private async Task<int> ProcessParsedSeries(bool forceUpdate, Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Library library, long scanElapsedTime)
    {
        // Iterate over the dictionary and remove only the ParserInfos that don't need processing
        var toProcess = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        var scanSw = Stopwatch.StartNew();

        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();

        foreach (var series in parsedSeries)
        {
            if (!series.Key.HasChanged)
            {
                _logger.LogDebug("{Series} hasn't changed", series.Key.Name);
                continue;
            }

            // Filter out ParserInfos where FullFilePath is empty (i.e., folder not modified)
            var validInfos = series.Value.Where(info => !string.IsNullOrEmpty(info.Filename)).ToList();

            if (validInfos.Count != 0)
            {
                toProcess[series.Key] = validInfos;
            }
        }

        if (toProcess.Count > 0)
        {
            // For all Genres in the ParserInfos, do a bulk check against the DB on what is not in the DB and create them
            // This will ensure all Genres are pre-created and allow our Genre lookup (and Priming) to be much simpler. It will be slower, but more consistent.
            var allGenres = toProcess
                .SelectMany(s => s.Value
                    .SelectMany(p => p.ComicInfo?.Genre?
                                         .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                     ?? []))
                .Distinct()
                .ToList();

            var allTags = toProcess
                .SelectMany(s => s.Value
                    .SelectMany(p => p.ComicInfo?.Tags?
                                         .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                     ?? []))
                .Distinct()
                .ToList();

            ExternalMetadataService.GenerateExternalGenreAndTagsList(allGenres, allTags, settings,
                out var processedTags, out var processedGenres);

            await CreateAllGenresAsync(processedGenres);
            await CreateAllTagsAsync(processedTags);
        }

        _logger.LogInformation("[ScannerService] Found {SeriesCount} Series that need processing in {Time} ms", toProcess.Count, scanSw.ElapsedMilliseconds + scanElapsedTime);

        var totalFiles = await ProcessParserInfo(settings, toProcess.Values.ToList(), library, forceUpdate);

        _logger.LogInformation("[ScannerService] Finished scan in {ScanAndUpdateTime} milliseconds.", scanSw.ElapsedMilliseconds + scanElapsedTime);

        return totalFiles;
    }

    /// <summary>
    /// Runs metadata updates (database heavy) and extra tasks (I/O heavy) in parallel
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="toProcess"></param>
    /// <param name="library"></param>
    /// <param name="forceUpdate"></param>
    /// <returns>Total amount of processed files</returns>
    private async Task<int> ProcessParserInfo(MetadataSettingsDto settings, IList<IList<ParserInfo>> toProcess, Library library, bool forceUpdate)
    {
        var channel = Channel.CreateUnbounded<int>();

        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var dbTask = Task.Run(async () => await DbMetadataTask(channel, settings, toProcess, library.Id, library.Name, forceUpdate));

        var amountOfProcessors = Environment.ProcessorCount;
        var usingCount = Math.Max(1, amountOfProcessors / 2);
        _logger.LogDebug("[ScannerService] Going to use {Cores} / {TotalCores} threads for I/O tasks this scan",
            usingCount, amountOfProcessors);

        IList<Task<long>> tasks = [];
        for (var i = 0; i < usingCount; i++)
        {
            tasks.Add(Task.Run(async () => await ExtraWorkTask(channel, serverSettings, library.Id, forceUpdate)));
        }

        tasks.Add(dbTask);

        await Task.WhenAll(tasks);

        var totalIoTime = tasks.Select(t => t.Result).Sum();
        var avgTimePerThread = totalIoTime / usingCount;
        _logger.LogDebug("[ScannerService] Spend {Elapsed}ms processing covers & word count, {Average}ms per thread",
            totalIoTime, avgTimePerThread);

        return (int) dbTask.Result;
    }

    /// <summary>
    /// A thread handling cover generation and word count. Completes when the channel completes
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="serverSettings"></param>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate"></param>
    /// <returns></returns>
    private async Task<long> ExtraWorkTask(Channel<int> channel, ServerSettingDto serverSettings, int libraryId, bool forceUpdate)
    {
        var sw = Stopwatch.StartNew();

        await foreach (var seriesId in channel.Reader.ReadAllAsync())
        {
            using var scope = _scopeFactory.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
            var wordCountAnalyzerService = scope.ServiceProvider.GetRequiredService<IWordCountAnalyzerService>();

            await metadataService.GenerateCoversForSeries(serverSettings, libraryId, seriesId, false, false);
            await wordCountAnalyzerService.ScanSeries(libraryId, seriesId, forceUpdate);
        }

        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Processes all founds series sequentially, and writes the seriesIds to the channel afterwards
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="settings"></param>
    /// <param name="toProcess"></param>
    /// <param name="libraryId"></param>
    /// <param name="libraryName"></param>
    /// <param name="forceUpdate"></param>
    /// <returns>The total amount of processed files</returns>
    private async Task<long> DbMetadataTask(Channel<int> channel, MetadataSettingsDto settings, IList<IList<ParserInfo>> toProcess, int libraryId, string libraryName, bool forceUpdate)
    {
        var totalFiles = 0;
        var seriesLeftToProcess = toProcess.Count;
        var totalSeriesToProcess = toProcess.Count;
        var sw = Stopwatch.StartNew();

        try
        {
            foreach (var pSeries in toProcess)
            {
                totalFiles += pSeries.Count;

                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var processSeries = scope.ServiceProvider.GetRequiredService<IProcessSeries>();

                // Library needs to be returned from the used UnitOfWork
                var library = (await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns))!;

                var seriesId = await processSeries.ProcessSeriesAsync(settings, pSeries, new ProcessSeriesArgs
                {
                    Library = library,
                    LeftToProcess = seriesLeftToProcess,
                    TotalToProcess = totalSeriesToProcess,
                    ForceUpdate = forceUpdate,
                });

                if (seriesId != null)
                {
                    await channel.Writer.WriteAsync(seriesId.Value);
                }

                seriesLeftToProcess--;
            }
        }
        finally // Ensure the channel is closed in case of an exception that we didn't expect
        {
            channel.Writer.Complete();
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.LibraryScanProgressEvent(string.Empty, libraryName, ProgressEventType.Ended));

        _logger.LogDebug("[ScannerService] Finished writing metadata for {Count} series in {Elapsed}ms", toProcess.Count, sw.ElapsedMilliseconds);

        return totalFiles;
    }

    private static void UpdateLastScanned(Library library)
    {
        var time = DateTime.Now;
        foreach (var folderPath in library.Folders)
        {
            folderPath.UpdateLastScanned(time);
        }

        library.UpdateLastScanned(time);
    }

    private async Task<Tuple<long, Dictionary<ParsedSeries, IList<ParserInfo>>>> ScanFiles(Library library, IList<string> dirs,
        bool isLibraryScan, bool forceChecks = false)
    {
        var scanner = new ParseScannedFiles(_logger, _directoryService, _readingItemService, _eventHub);
        var scanWatch = Stopwatch.StartNew();

        var processedSeries = await scanner.ScanLibrariesForSeries(library, dirs,
            isLibraryScan, await _unitOfWork.SeriesRepository.GetFolderPathMap(library.Id), forceChecks);

        var scanElapsedTime = scanWatch.ElapsedMilliseconds;

        var parsedSeries = TrackFoundSeriesAndFiles(processedSeries);

        return Tuple.Create(scanElapsedTime, parsedSeries);
    }

    /// <summary>
    /// Given a list of all Genres, generates new Genre entries for any that do not exist.
    /// Does not delete anything, that will be handled by nightly task
    /// </summary>
    /// <param name="genres"></param>
    private async Task CreateAllGenresAsync(ICollection<string> genres)
    {
        _logger.LogInformation("[ScannerService] Attempting to pre-save all Genres");

        try
        {
            // Pass the non-normalized genres directly to the repository
            var nonExistingGenres = await _unitOfWork.GenreRepository.GetAllGenresNotInListAsync(genres);

            // Create and attach new genres using the non-normalized names
            foreach (var genre in nonExistingGenres)
            {
                var newGenre = new GenreBuilder(genre).Build();
                _unitOfWork.GenreRepository.Attach(newGenre);
            }

            // Commit changes
            if (nonExistingGenres.Count > 0)
            {
                await _unitOfWork.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an unknown issue when pre-saving all Genres");
        }
    }

    /// <summary>
    /// Given a list of all Tags, generates new Tag entries for any that do not exist.
    /// Does not delete anything, that will be handled by nightly task
    /// </summary>
    /// <param name="tags"></param>
    private async Task CreateAllTagsAsync(ICollection<string> tags)
    {
        _logger.LogInformation("[ScannerService] Attempting to pre-save all Tags");

        try
        {
            // Pass the non-normalized tags directly to the repository
            var nonExistingTags = await _unitOfWork.TagRepository.GetAllTagsNotInListAsync(tags);

            // Create and attach new genres using the non-normalized names
            foreach (var tag in nonExistingTags)
            {
                var newTag = new TagBuilder(tag).Build();
                _unitOfWork.TagRepository.Attach(newTag);
            }

            // Commit changes
            if (nonExistingTags.Count > 0)
            {
                await _unitOfWork.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] There was an unknown issue when pre-saving all Tags");
        }
    }
}
