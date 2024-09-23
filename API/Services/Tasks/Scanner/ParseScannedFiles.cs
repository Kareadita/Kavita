using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using ExCSS;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;
#nullable enable

public class ParsedSeries
{
    /// <summary>
    /// Name of the Series
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Normalized Name of the Series
    /// </summary>
    public required string NormalizedName { get; init; }
    /// <summary>
    /// Format of the Series
    /// </summary>
    public required MangaFormat Format { get; init; }
}

public class ScanResult
{
    /// <summary>
    /// A list of files in the Folder. Empty if HasChanged = false
    /// </summary>
    public IList<string> Files { get; set; }
    /// <summary>
    /// A nested folder from Library Root (at any level)
    /// </summary>
    public string Folder { get; set; }
    /// <summary>
    /// The library root
    /// </summary>
    public string LibraryRoot { get; set; }
    /// <summary>
    /// Was the Folder scanned or not. If not modified since last scan, this will be false and Files empty
    /// </summary>
    public bool HasChanged { get; set; }
    /// <summary>
    /// Set in Stage 2: Parsed Info from the Files
    /// </summary>
    public IList<ParserInfo> ParserInfos { get; set; }
}

/// <summary>
/// The final product of ParseScannedFiles. This has all the processed parserInfo and is ready for tracking/processing into entities
/// </summary>
public class ScannedSeriesResult
{
    /// <summary>
    /// Was the Folder scanned or not. If not modified since last scan, this will be false and indicates that upstream should count this as skipped
    /// </summary>
    public bool HasChanged { get; set; }
    /// <summary>
    /// The Parsed Series information used for tracking
    /// </summary>
    public ParsedSeries ParsedSeries { get; set; }
    /// <summary>
    /// Parsed files
    /// </summary>
    public IList<ParserInfo> ParsedInfos { get; set; }
}

public class SeriesModified
{
    public required string? FolderPath { get; set; }
    public required string? LowestFolderPath { get; set; }
    public required string SeriesName { get; set; }
    public DateTime LastScanned { get; set; }
    public MangaFormat Format { get; set; }
    public IEnumerable<string> LibraryRoots { get; set; } = ArraySegment<string>.Empty;
}

/// <summary>
/// Responsible for taking parsed info from ReadingItemService and DirectoryService and combining them to emit DB work
/// on a series by series.
/// </summary>
public class ParseScannedFiles
{
    private readonly ILogger _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IEventHub _eventHub;

    /// <summary>
    /// An instance of a pipeline for processing files and returning a Map of Series -> ParserInfos.
    /// Each instance is separate from other threads, allowing for no cross over.
    /// </summary>
    /// <param name="logger">Logger of the parent class that invokes this</param>
    /// <param name="directoryService">Directory Service</param>
    /// <param name="readingItemService">ReadingItemService Service for extracting information on a number of formats</param>
    /// <param name="eventHub">For firing off SignalR events</param>
    public ParseScannedFiles(ILogger logger, IDirectoryService directoryService,
        IReadingItemService readingItemService, IEventHub eventHub)
    {
        _logger = logger;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _eventHub = eventHub;
    }

    /// <summary>
    /// This will Scan all files in a folder path. For each folder within the folderPath, FolderAction will be invoked for all files contained
    /// </summary>
    /// <param name="scanDirectoryByDirectory">Scan directory by directory and for each, call folderAction</param>
    /// <param name="seriesPaths">A dictionary mapping a normalized path to a list of <see cref="SeriesModified"/> to help scanner skip I/O</param>
    /// <param name="folderPath">A library folder or series folder</param>
    /// <param name="forceCheck">If we should bypass any folder last write time checks on the scan and force I/O</param>
    public async Task<IList<ScanResult>> ProcessFiles(string folderPath, bool scanDirectoryByDirectory,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Library library, bool forceCheck = false)
    {
        var fileExtensions = string.Join("|", library.LibraryFileTypes.Select(l => l.FileTypeGroup.GetRegex()));
        var matcher = BuildMatcher(library);

        var result = new List<ScanResult>();

        // Not to self: this whole thing can be parallelized because we don't deal with any DB or global state
        if (scanDirectoryByDirectory)
        {
            return await ScanDirectories(folderPath, seriesPaths, library, forceCheck, matcher, result, fileExtensions);
        }

        return await ScanSingleDirectory(folderPath, seriesPaths, library, forceCheck, result, fileExtensions, matcher);
    }

    /// <summary>
    /// Scans directories and processes changes, optimizing for Publisher/Series folder structure.
    /// Detects both folder changes and new folders, ensuring appropriate scanning.
    /// </summary>
    private async Task<IList<ScanResult>> ScanDirectories(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths,
        Library library, bool forceCheck, GlobMatcher matcher, List<ScanResult> result, string fileExtensions)
    {
        var directories = _directoryService.GetDirectories(folderPath, matcher).Select(Parser.Parser.NormalizePath);

        foreach (var directory in directories)
        {
            var sw = Stopwatch.StartNew();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(directory, library.Name, ProgressEventType.Updated));

            if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, directory, forceCheck))
            {
                HandleUnchangedFolder(result, folderPath, directory);
            }
            else if (!forceCheck && ShouldOptimizeForSeries(seriesPaths, directory))
            {
                await HandleMultipleSeriesFolders(result, seriesPaths[directory], directory, library.Name, folderPath, fileExtensions, matcher);
            }
            else
            {
                PerformFullScan(result, directory, folderPath, fileExtensions, matcher);
            }
            _logger.LogDebug("Processing {Directory} took {TimeMs}ms to check", directory, sw.ElapsedMilliseconds);
        }

        return result;
    }



    /// <summary>
    /// Handles directories that haven't changed since the last scan.
    /// </summary>
    private void HandleUnchangedFolder(List<ScanResult> result, string folderPath, string directory)
    {
        if (result.Exists(r => r.Folder == directory))
        {
            _logger.LogDebug("[ProcessFiles] Skipping adding {Directory} as it's already added", directory);
        }
        else
        {
            _logger.LogDebug("[ProcessFiles] Skipping {Directory} as it hasn't changed since last scan", directory);
            result.Add(CreateScanResult(directory, folderPath, false, ArraySegment<string>.Empty));
        }
    }

    /// <summary>
    /// Optimizes the scan for folders containing multiple series by checking if specific series folders have changed.
    /// </summary>
    private async Task HandleMultipleSeriesFolders(List<ScanResult> result, IList<SeriesModified> seriesList,
        string directory, string libraryName, string folderPath, string fileExtensions, GlobMatcher matcher)
    {
        _logger.LogDebug("[ProcessFiles] {Directory} is dirty and has multiple series folders, checking if we can avoid a full scan", directory);

        foreach (var seriesModified in seriesList)
        {
            var hasFolderChangedSinceLastScan = seriesModified.LastScanned.Truncate(TimeSpan.TicksPerSecond) <
                                                _directoryService.GetLastWriteTime(seriesModified.LowestFolderPath!).Truncate(TimeSpan.TicksPerSecond);

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(seriesModified.LowestFolderPath!, libraryName, ProgressEventType.Updated));

            if (!hasFolderChangedSinceLastScan)
            {
                DetectNewFoldersAndHandleUnchanged(result, seriesModified, folderPath, matcher, fileExtensions, directory);
            }
            else
            {
                _logger.LogDebug("[ProcessFiles] {Directory} subfolder {Folder} changed for Series {SeriesName}",
                    directory, seriesModified.LowestFolderPath, seriesModified.SeriesName);
                var files = _directoryService.ScanFiles(seriesModified.LowestFolderPath!, fileExtensions, matcher);
                result.Add(CreateScanResult(seriesModified.LowestFolderPath!, folderPath, true, files));
            }
        }
    }

    /// <summary>
    /// Detects new folders that were added to a series and handles them.
    /// </summary>
    private void DetectNewFoldersAndHandleUnchanged(List<ScanResult> result, SeriesModified seriesModified,
        string folderPath, GlobMatcher matcher, string fileExtensions, string directory)
    {
        var currentSubdirectories = _directoryService.GetDirectories(directory, matcher).Select(Parser.Parser.NormalizePath);
        var knownSeriesPaths = seriesModified.LibraryRoots.Select(Parser.Parser.NormalizePath).ToList();

        // Check if there are any new folders that aren't part of the known series
        var newFolders = currentSubdirectories.Except(knownSeriesPaths).ToList();

        if (newFolders.Count != 0)
        {
            _logger.LogDebug("[ProcessFiles] New folders detected in {Directory}, scanning new folders", directory);
            foreach (var newFolder in newFolders)
            {
                var files = _directoryService.ScanFiles(newFolder, fileExtensions, matcher);
                _logger.LogDebug("[ProcessFiles] {Directory} contains {FilesCount} files", directory, files.Count);
                result.Add(CreateScanResult(newFolder, folderPath, true, files));
            }
        }
        else
        {
            _logger.LogDebug("[ProcessFiles] {Directory} subfolder {Folder} did not change and no new folders detected, skipping",
                directory, seriesModified.LowestFolderPath);
            result.Add(CreateScanResult(seriesModified.LowestFolderPath!, folderPath, false, ArraySegment<string>.Empty));
        }
    }


    /// <summary>
    /// Checks if the directory can be optimized for scanning by checking individual series folders.
    /// </summary>
    private static bool ShouldOptimizeForSeries(IDictionary<string, IList<SeriesModified>> seriesPaths, string directory)
    {
        return seriesPaths.TryGetValue(directory, out var series) && series.Count > 1 && series.All(s => !string.IsNullOrEmpty(s.LowestFolderPath));
    }

    /// <summary>
    /// Performs a full scan of the directory and adds it to the result.
    /// </summary>
    private void PerformFullScan(List<ScanResult> result, string directory, string folderPath, string fileExtensions, GlobMatcher matcher)
    {
        _logger.LogDebug("[ProcessFiles] Performing full scan on {Directory}", directory);
        var files = _directoryService.ScanFiles(directory, fileExtensions, matcher);
        result.Add(CreateScanResult(directory, folderPath, true, files));
    }



    private async Task<IList<ScanResult>> ScanDirectories2(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths, Library library, bool forceCheck,
        GlobMatcher matcher, List<ScanResult> result, string fileExtensions)
    {
        var directories = _directoryService.GetDirectories(folderPath, matcher).Select(Parser.Parser.NormalizePath);
        foreach (var directory in directories)
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(directory, library.Name, ProgressEventType.Updated));


            if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, directory, forceCheck))
            {
                if (result.Exists(r => r.Folder == directory))
                {
                    _logger.LogDebug("[ProcessFiles] Skipping adding {Directory} as it's already added", directory);
                    continue;
                }
                _logger.LogDebug("[ProcessFiles] Skipping {Directory} as it hasn't changed since last scan", directory);
                result.Add(CreateScanResult(directory, folderPath, false, ArraySegment<string>.Empty));
            }
            else if (!forceCheck && seriesPaths.TryGetValue(directory, out var series)
                                 && series.Count > 1 && series.All(s => !string.IsNullOrEmpty(s.LowestFolderPath)))
            {
                // If there are multiple series inside this path, let's check each of them to see which was modified and only scan those
                // This is very helpful for ComicVine libraries by Publisher

                // TODO: BUG: We might miss new folders this way. Likely need to get all folder names and see if there are any that aren't in known series list

                _logger.LogDebug("[ProcessFiles] {Directory} is dirty and has multiple series folders, checking if we can avoid a full scan", directory);
                foreach (var seriesModified in series)
                {
                    var hasFolderChangedSinceLastScan = seriesModified.LastScanned.Truncate(TimeSpan.TicksPerSecond) <
                                                        _directoryService
                                                            .GetLastWriteTime(seriesModified.LowestFolderPath!)
                                                            .Truncate(TimeSpan.TicksPerSecond);

                    await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                        MessageFactory.FileScanProgressEvent(seriesModified.LowestFolderPath!, library.Name, ProgressEventType.Updated));

                    if (!hasFolderChangedSinceLastScan)
                    {
                        _logger.LogDebug("[ProcessFiles] {Directory} subfolder {Folder} did not change since last scan, adding entry to skip", directory, seriesModified.LowestFolderPath);
                        result.Add(CreateScanResult(seriesModified.LowestFolderPath!, folderPath, false, ArraySegment<string>.Empty));
                    }
                    else
                    {
                        _logger.LogDebug("[ProcessFiles] {Directory} subfolder {Folder} changed for Series {SeriesName}", directory, seriesModified.LowestFolderPath, seriesModified.SeriesName);
                        result.Add(CreateScanResult(directory, folderPath, true,
                            _directoryService.ScanFiles(seriesModified.LowestFolderPath!, fileExtensions, matcher)));
                    }
                }
            }
            else
            {
                _logger.LogDebug("[ProcessFiles] Performing file scan on {Directory}", directory);
                var files = _directoryService.ScanFiles(directory, fileExtensions, matcher);
                result.Add(CreateScanResult(directory, folderPath, true, files));
            }
        }

        return result;
    }

    /// <summary>
    /// Scans a single directory and processes the scan result.
    /// </summary>
    private async Task<IList<ScanResult>> ScanSingleDirectory(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths, Library library, bool forceCheck, List<ScanResult> result,
        string fileExtensions, GlobMatcher matcher)
    {
        var normalizedPath = Parser.Parser.NormalizePath(folderPath);
        var libraryRoot =
            library.Folders.FirstOrDefault(f =>
                normalizedPath.Contains(Parser.Parser.NormalizePath(f.Path)))?.Path ??
            folderPath;

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent(normalizedPath, library.Name, ProgressEventType.Updated));

        if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedPath, forceCheck))
        {
            result.Add(CreateScanResult(folderPath, libraryRoot, false, ArraySegment<string>.Empty));
        }
        else
        {
            result.Add(CreateScanResult(folderPath, libraryRoot, true,
                _directoryService.ScanFiles(folderPath, fileExtensions, matcher)));
        }

        return result;
    }

    private static GlobMatcher BuildMatcher(Library library)
    {
        var matcher = new GlobMatcher();
        foreach (var pattern in library.LibraryExcludePatterns.Where(p => !string.IsNullOrEmpty(p.Pattern)))
        {
            matcher.AddExclude(pattern.Pattern);
        }

        return matcher;
    }

    private static ScanResult CreateScanResult(string folderPath, string libraryRoot, bool hasChanged,
        IList<string> files)
    {
        return new ScanResult()
        {
            Files = files,
            Folder = Parser.Parser.NormalizePath(folderPath),
            LibraryRoot = libraryRoot,
            HasChanged = hasChanged
        };
    }


    /// <summary>
    /// Attempts to either add a new instance of a series mapping to the _scannedSeries bag or adds to an existing.
    /// This will check if the name matches an existing series name (multiple fields) <see cref="MergeName"/>
    /// </summary>
    /// <param name="scannedSeries">A localized list of a series' parsed infos</param>
    /// <param name="info"></param>
    private void TrackSeries(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParserInfo? info)
    {
        if (info == null || info.Series == string.Empty) return;

        // Check if normalized info.Series already exists and if so, update info to use that name instead
        info.Series = MergeName(scannedSeries, info);

        var normalizedSeries = info.Series.ToNormalized();
        var normalizedSortSeries = info.SeriesSort.ToNormalized();
        var normalizedLocalizedSeries = info.LocalizedSeries.ToNormalized();

        try
        {
            var existingKey = scannedSeries.Keys.SingleOrDefault(ps =>
                ps.Format == info.Format && (ps.NormalizedName.Equals(normalizedSeries)
                                             || ps.NormalizedName.Equals(normalizedLocalizedSeries)
                                             || ps.NormalizedName.Equals(normalizedSortSeries)));
            existingKey ??= new ParsedSeries()
            {
                Format = info.Format,
                Name = info.Series,
                NormalizedName = normalizedSeries
            };

            scannedSeries.AddOrUpdate(existingKey, [info], (_, oldValue) =>
            {
                oldValue ??= new List<ParserInfo>();
                if (!oldValue.Contains(info))
                {
                    oldValue.Add(info);
                }

                return oldValue;
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[ScannerService] {SeriesName} matches against multiple series in the parsed series. This indicates a critical kavita issue. Key will be skipped", info.Series);
            foreach (var seriesKey in scannedSeries.Keys.Where(ps =>
                         ps.Format == info.Format && (ps.NormalizedName.Equals(normalizedSeries)
                                                      || ps.NormalizedName.Equals(normalizedLocalizedSeries)
                                                      || ps.NormalizedName.Equals(normalizedSortSeries))))
            {
                _logger.LogCritical("[ScannerService] Matches: {SeriesName} matches on {SeriesKey}", info.Series, seriesKey.Name);
            }
        }
    }


    /// <summary>
    /// Using a normalized name from the passed ParserInfo, this checks against all found series so far and if an existing one exists with
    /// same normalized name, it merges into the existing one. This is important as some manga may have a slight difference with punctuation or capitalization.
    /// </summary>
    /// <param name="scannedSeries"></param>
    /// <param name="info"></param>
    /// <returns>Series Name to group this info into</returns>
    private string MergeName(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParserInfo info)
    {
        var normalizedSeries = info.Series.ToNormalized();
        var normalizedLocalSeries = info.LocalizedSeries.ToNormalized();

        try
        {
            var existingName =
                scannedSeries.SingleOrDefault(p =>
                        (p.Key.NormalizedName.ToNormalized().Equals(normalizedSeries) ||
                         p.Key.NormalizedName.ToNormalized().Equals(normalizedLocalSeries)) &&
                        p.Key.Format == info.Format)
                    .Key;

            if (existingName == null)
            {
                return info.Series;
            }

            if (!string.IsNullOrEmpty(existingName.Name))
            {
                return existingName.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[ScannerService] Multiple series detected for {SeriesName} ({File})! This is critical to fix! There should only be 1", info.Series, info.FullFilePath);
            var values = scannedSeries.Where(p =>
                (p.Key.NormalizedName.ToNormalized() == normalizedSeries ||
                 p.Key.NormalizedName.ToNormalized() == normalizedLocalSeries) &&
                p.Key.Format == info.Format);

            foreach (var pair in values)
            {
                _logger.LogCritical("[ScannerService] Duplicate Series in DB matches with {SeriesName}: {DuplicateName}", info.Series, pair.Key.Name);
            }

        }

        return info.Series;
    }


    /// <summary>
    /// This will process series by folder groups. This is used solely by ScanSeries
    /// </summary>
    /// <param name="library">This should have the FileTypes included</param>
    /// <param name="folders"></param>
    /// <param name="isLibraryScan">If true, does a directory scan first (resulting in folders being tackled in parallel), else does an immediate scan files</param>
    /// <param name="seriesPaths">A map of Series names -> existing folder paths to handle skipping folders</param>
    /// <param name="forceCheck">Defaults to false</param>
    /// <returns></returns>
    // public async Task<IList<ScannedSeriesResult>> ScanLibrariesForSeriesOld(Library library,
    //     IEnumerable<string> folders, bool isLibraryScan,
    //     IDictionary<string, IList<SeriesModified>> seriesPaths, bool forceCheck = false)
    // {
    //     await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Starting", library.Name, ProgressEventType.Started));
    //
    //     _logger.LogDebug("[ScannerService] Library {LibraryName} Step 1.A: Process {FolderCount} folders", library.Name, folders.Count());
    //     var processedScannedSeries = new List<ScannedSeriesResult>();
    //     //var processedScannedSeries = new ConcurrentBag<ScannedSeriesResult>();
    //     foreach (var folderPath in folders)
    //     {
    //         try
    //         {
    //             _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.B: Scan files in {Folder}", library.Name, folderPath);
    //             var scanResults = await ProcessFiles(folderPath, isLibraryScan, seriesPaths, library, forceCheck);
    //
    //             _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.C: Process files in {Folder}", library.Name, folderPath);
    //             foreach (var scanResult in scanResults)
    //             {
    //                 await ParseAndTrackSeries(library, seriesPaths, scanResult, processedScannedSeries);
    //             }
    //
    //             // This reduced a 1.1k series networked scan by a little more than 1 hour, but the order series were added to Kavita was not alphabetical
    //             // await Task.WhenAll(scanResults.Select(async scanResult =>
    //             // {
    //             //     await ParseAndTrackSeries(library, seriesPaths, scanResult, processedScannedSeries);
    //             // }));
    //
    //         }
    //         catch (ArgumentException ex)
    //         {
    //             _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folderPath);
    //         }
    //     }
    //
    //     await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Done", library.Name, ProgressEventType.Ended));
    //
    //     return processedScannedSeries.ToList();
    //
    // }

    /// <summary>
    /// This will process series by folder groups. This is used solely by ScanSeries
    /// </summary>
    /// <param name="library">This should have the FileTypes included</param>
    /// <param name="folders"></param>
    /// <param name="isLibraryScan">If true, does a directory scan first (resulting in folders being tackled in parallel), else does an immediate scan files</param>
    /// <param name="seriesPaths">A map of Series names -> existing folder paths to handle skipping folders</param>
    /// <param name="forceCheck">Defaults to false</param>
    /// <returns></returns>
    public async Task<IList<ScannedSeriesResult>> ScanLibrariesForSeries(Library library,
        IEnumerable<string> folders, bool isLibraryScan,
        IDictionary<string, IList<SeriesModified>> seriesPaths, bool forceCheck = false)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent("File Scan Starting", library.Name, ProgressEventType.Started));

        _logger.LogDebug("[ScannerService] Library {LibraryName} Step 1.A: Process {FolderCount} folders", library.Name, folders.Count());
        var processedScannedSeries = new ConcurrentBag<ScannedSeriesResult>();

        try
        {
            await Parallel.ForEachAsync(folders, async (folderPath, cancellationToken) =>
            {
                try
                {
                    await ScanAndParseFolder(folderPath, library, isLibraryScan, seriesPaths, processedScannedSeries, forceCheck);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folderPath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScannerService] Error occurred while processing folders");
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent("File Scan Done", library.Name, ProgressEventType.Ended));

        return processedScannedSeries.ToList();
    }

    /// <summary>
    /// Helper method to scan and parse a folder
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="library"></param>
    /// <param name="isLibraryScan"></param>
    /// <param name="seriesPaths"></param>
    /// <param name="processedScannedSeries"></param>
    /// <param name="forceCheck"></param>
    private async Task ScanAndParseFolder(string folderPath, Library library,
        bool isLibraryScan, IDictionary<string, IList<SeriesModified>> seriesPaths,
        ConcurrentBag<ScannedSeriesResult> processedScannedSeries, bool forceCheck)
    {
        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.B: Scan files in {Folder}", library.Name, folderPath);
        var scanResults = await ProcessFiles(folderPath, isLibraryScan, seriesPaths, library, forceCheck);

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.C: Process files in {Folder}", library.Name, folderPath);
        foreach (var scanResult in scanResults)
        {
            await ParseAndTrackSeries(library, seriesPaths, scanResult, processedScannedSeries);
        }
    }

    /// <summary>
    /// Parses and tracks series from scan results
    /// </summary>
    /// <param name="library"></param>
    /// <param name="seriesPaths"></param>
    /// <param name="scanResult"></param>
    /// <param name="processedScannedSeries"></param>
    private async Task ParseAndTrackSeries(Library library, IDictionary<string, IList<SeriesModified>> seriesPaths, ScanResult scanResult,
        ConcurrentBag<ScannedSeriesResult> processedScannedSeries)
    {
        // scanResult is updated with the parsed info
        await ProcessScanResult(scanResult, seriesPaths, library);

        // Perform any merging that is necessary and post processing steps
        var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();

        // Merge localized series (like Nagatoro/nagator.cbz, japanesename.cbz) -> Nagatoro series
        MergeLocalizedSeriesWithSeries(scanResult.ParserInfos);

        // Combine everything into scannedSeries
        foreach (var info in scanResult.ParserInfos)
        {
            try
            {
                TrackSeries(scannedSeries, info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ScannerService] Exception occurred during tracking {FilePath}. Skipping this file", info?.FullFilePath);
            }
        }

        foreach (var series in scannedSeries.Keys)
        {
            if (scannedSeries[series].Count <= 0) continue;

            try
            {
                UpdateSortOrder(scannedSeries, series);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ScannerService] Issue occurred while setting IssueOrder for series {SeriesName}", series.Name);
            }

            processedScannedSeries.Add(new ScannedSeriesResult()
            {
                HasChanged = scanResult.HasChanged,
                ParsedSeries = series,
                ParsedInfos = scannedSeries[series]
            });
        }
    }




    // private async Task ParseAndTrackSeries(Library library, IDictionary<string, IList<SeriesModified>> seriesPaths, ScanResult scanResult,
    //     List<ScannedSeriesResult> processedScannedSeries)
    // {
    //     // scanResult is updated with the parsed infos
    //     await ProcessScanResult(scanResult, seriesPaths, library); // NOTE: This may be able to be parallelized
    //
    //     // We now have all the parsed infos from the scan result, perform any merging that is necessary and post processing steps
    //     var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
    //
    //     // Merge any series together (like Nagatoro/nagator.cbz, japanesename.cbz) -> Nagator series
    //     MergeLocalizedSeriesWithSeries(scanResult.ParserInfos);
    //
    //     // Combine everything into scannedSeries
    //     foreach (var info in scanResult.ParserInfos)
    //     {
    //         try
    //         {
    //             TrackSeries(scannedSeries, info);
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogError(ex,
    //                 "[ScannerService] There was an exception that occurred during tracking {FilePath}. Skipping this file",
    //                 info?.FullFilePath);
    //         }
    //     }
    //
    //     foreach (var series in scannedSeries.Keys)
    //     {
    //         if (scannedSeries[series].Count <= 0) continue;
    //
    //         try
    //         {
    //             UpdateSortOrder(scannedSeries, series);
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogError(ex, "There was an issue setting IssueOrder");
    //         }
    //
    //
    //         processedScannedSeries.Add(new ScannedSeriesResult()
    //         {
    //             HasChanged = scanResult.HasChanged,
    //             ParsedSeries = series,
    //             ParsedInfos = scannedSeries[series]
    //         });
    //     }
    // }

    // /// <summary>
    // /// For a given ScanResult, sets the ParserInfos on the result
    // /// </summary>
    // /// <param name="result"></param>
    // /// <param name="seriesPaths"></param>
    // /// <param name="library"></param>
    // private async Task ProcessScanResult(ScanResult result, IDictionary<string, IList<SeriesModified>> seriesPaths, Library library)
    // {
    //     // TODO: This should return the result as we are modifying it as a side effect
    //
    //     // If the folder hasn't changed, generate fake ParserInfos for the Series that were in that folder.
    //     var normalizedFolder = Parser.Parser.NormalizePath(result.Folder);
    //     if (!result.HasChanged)
    //     {
    //         result.ParserInfos = seriesPaths[normalizedFolder]
    //             .Select(fp => new ParserInfo()
    //             {
    //                 Series = fp.SeriesName,
    //                 Format = fp.Format,
    //             })
    //             .ToList();
    //
    //         _logger.LogDebug("[ScannerService] Skipped File Scan for {Folder} as it hasn't changed since last scan", normalizedFolder);
    //         await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
    //             MessageFactory.FileScanProgressEvent("Skipped " + normalizedFolder, library.Name, ProgressEventType.Updated));
    //         return;
    //     }
    //
    //     var files = result.Files;
    //
    //     // When processing files for a folder and we do enter, we need to parse the information and combine parser infos
    //     // NOTE: We might want to move the merge step later in the process, like return and combine.
    //
    //     if (files.Count == 0)
    //     {
    //         _logger.LogInformation("[ScannerService] {Folder} is empty, no longer in this location, or has no file types that match Library File Types", normalizedFolder);
    //         result.ParserInfos = ArraySegment<ParserInfo>.Empty;
    //         return;
    //     }
    //
    //     _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, normalizedFolder);
    //     await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
    //         MessageFactory.FileScanProgressEvent($"{files.Count} files in {normalizedFolder}", library.Name, ProgressEventType.Updated));
    //
    //     // Multiple Series can exist within a folder. We should instead put these infos on the result and perform merging above
    //     IList<ParserInfo> infos = files
    //         .Select(file => _readingItemService.ParseFile(file, normalizedFolder, result.LibraryRoot, library.Type))
    //         .Where(info => info != null)
    //         .ToList()!;
    //
    //     result.ParserInfos = infos;
    // }

    /// <summary>
    /// For a given ScanResult, sets the ParserInfos on the result
    /// </summary>
    /// <param name="result"></param>
    /// <param name="seriesPaths"></param>
    /// <param name="library"></param>
    private async Task ProcessScanResult(ScanResult result, IDictionary<string, IList<SeriesModified>> seriesPaths, Library library)
    {
        var normalizedFolder = Parser.Parser.NormalizePath(result.Folder);

        // If folder hasn't changed, generate fake ParserInfos
        if (!result.HasChanged)
        {
            result.ParserInfos = seriesPaths[normalizedFolder]
                .Select(fp => new ParserInfo { Series = fp.SeriesName, Format = fp.Format })
                .ToList();

            _logger.LogDebug("[ScannerService] Skipped File Scan for {Folder} as it hasn't changed", normalizedFolder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent("Skipped " + normalizedFolder, library.Name, ProgressEventType.Updated));
            return;
        }

        var files = result.Files;

        if (files.Count == 0)
        {
            _logger.LogInformation("[ScannerService] {Folder} is empty or has no matching file types", normalizedFolder);
            result.ParserInfos = ArraySegment<ParserInfo>.Empty;
            return;
        }

        _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, normalizedFolder);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent($"{files.Count} files in {normalizedFolder}", library.Name, ProgressEventType.Updated));

        // Parse files into ParserInfos
        result.ParserInfos = files
            .Select(file => _readingItemService.ParseFile(file, normalizedFolder, result.LibraryRoot, library.Type))
            .Where(info => info != null)
            .ToList()!;
    }


    private static void UpdateSortOrder(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParsedSeries series)
    {
        // Set the Sort order per Volume
        var volumes = scannedSeries[series].GroupBy(info => info.Volumes);
        foreach (var volume in volumes)
        {
            var infos = scannedSeries[series].Where(info => info.Volumes == volume.Key).ToList();
            IList<ParserInfo> chapters;
            var specialTreatment = infos.TrueForAll(info => info.IsSpecial);
            var hasAnySpMarker = infos.Exists(info => info.SpecialIndex > 0);
            var counter = 0f;

            // Handle specials with SpecialIndex
            if (specialTreatment && hasAnySpMarker)
            {
                chapters = infos
                    .OrderBy(info => info.SpecialIndex)
                    .ToList();

                foreach (var chapter in chapters)
                {
                    chapter.IssueOrder = counter;
                    counter++;
                }
                continue;
            }

            // Handle specials without SpecialIndex (natural order)
            if (specialTreatment)
            {
                chapters = infos
                    .OrderByNatural(info => Parser.Parser.RemoveExtensionIfSupported(info.Filename)!)
                    .ToList();

                foreach (var chapter in chapters)
                {
                    chapter.IssueOrder = counter;
                    counter++;
                }
                continue;
            }

            // Ensure chapters are sorted numerically when possible, otherwise push unparseable to the end
            chapters = infos
                .OrderBy(info => float.TryParse(info.Chapters, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : float.MaxValue)
                .ToList();

            counter = 0f;
            var prevIssue = string.Empty;
            foreach (var chapter in chapters)
            {
                if (float.TryParse(chapter.Chapters, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedChapter))
                {
                    // Parsed successfully, use the numeric value
                    counter = parsedChapter;
                    chapter.IssueOrder = counter;

                    // Increment for next chapter (unless the next has a similar value, then add 0.1)
                    if (!string.IsNullOrEmpty(prevIssue) && float.TryParse(prevIssue, CultureInfo.InvariantCulture, out var prevIssueFloat) && parsedChapter.Is(prevIssueFloat))
                    {
                        counter += 0.1f; // bump if same value as the previous issue
                    }
                    prevIssue = $"{parsedChapter}";
                }
                else
                {
                    // Unparsed chapters: use the current counter and bump for the next
                    if (!string.IsNullOrEmpty(prevIssue) && prevIssue == counter.ToString(CultureInfo.InvariantCulture))
                    {
                        counter += 0.1f; // bump if same value as the previous issue
                    }
                    chapter.IssueOrder = counter;
                    counter++;
                    prevIssue = chapter.Chapters;
                }
            }
        }
    }

    /// <summary>
    /// Checks against all folder paths on file if the last scanned is >= the directory's last write time, down to the second
    /// </summary>
    /// <param name="seriesPaths"></param>
    /// <param name="normalizedFolder"></param>
    /// <param name="forceCheck"></param>
    /// <returns></returns>
    private bool HasSeriesFolderNotChangedSinceLastScan(IDictionary<string, IList<SeriesModified>> seriesPaths, string normalizedFolder, bool forceCheck = false)
    {
        if (forceCheck) return false;

        return seriesPaths.TryGetValue(normalizedFolder, out var v) && HasAllSeriesFolderNotChangedSinceLastScan(v, normalizedFolder);
    }

    private bool HasAllSeriesFolderNotChangedSinceLastScan(IList<SeriesModified> seriesFolders,
        string normalizedFolder)
    {
        return seriesFolders.All(f => HasSeriesFolderNotChangedSinceLastScan(f, normalizedFolder));
    }

    private bool HasSeriesFolderNotChangedSinceLastScan(SeriesModified seriesModified, string normalizedFolder)
    {
        return _directoryService.Exists(normalizedFolder) && seriesModified.LastScanned.Truncate(TimeSpan.TicksPerSecond) >=
               _directoryService.GetLastWriteTime(normalizedFolder)
                   .Truncate(TimeSpan.TicksPerSecond);
    }



    /// <summary>
    /// Checks if there are any ParserInfos that have a Series that matches the LocalizedSeries field in any other info.
    /// If so, rewrites the infos with the series name instead of the localized name, so they stack.
    /// </summary>
    /// <example>
    /// Accel World v01.cbz has Series "Accel World" and Localized Series "World of Acceleration"
    /// World of Acceleration v02.cbz has Series "World of Acceleration"
    /// After running this code, we'd have:
    /// World of Acceleration v02.cbz having Series "Accel World" and Localized Series of "World of Acceleration"
    /// </example>
    /// <param name="infos">A collection of ParserInfos</param>
    private void MergeLocalizedSeriesWithSeries(IList<ParserInfo> infos)
    {
        // Filter relevant infos (non-special and with localized series)
        var relevantInfos = infos.Where(i => !i.IsSpecial && !string.IsNullOrEmpty(i.LocalizedSeries)).ToList();
        if (relevantInfos.Count == 0) return;

        // Get the first distinct localized series
        var localizedSeries = relevantInfos
            .Select(i => i.LocalizedSeries)
            .Distinct()
            .FirstOrDefault();
        if (string.IsNullOrEmpty(localizedSeries)) return;

        // Find non-localized series, normalizing by capitalization
        var distinctSeries = infos
            .Where(i => !i.IsSpecial)
            .Select(i => Parser.Parser.Normalize(i.Series))
            .Distinct()
            .ToList();

        if (distinctSeries.Count == 0) return;

        string? nonLocalizedSeries = null;

        switch (distinctSeries.Count)
        {
            // Handle the case where there is exactly one non-localized series
            case 1:
                nonLocalizedSeries = distinctSeries[0];
                break;
            case <= 2:
                // Look for a non-localized series different from the localized one
                nonLocalizedSeries = distinctSeries.FirstOrDefault(s => !s.Equals(Parser.Parser.Normalize(localizedSeries)));
                break;
            default:
                // Log an error when there are more than 2 distinct series in the folder
                _logger.LogError(
                    "[ScannerService] Multiple series detected within one folder that contain localized series. This will cause them to group incorrectly. Please separate series into their own dedicated folder or ensure there is only 2 potential series (localized and series): {LocalizedSeries}",
                    string.Join(", ", distinctSeries)
                );
                break;
        }

        if (nonLocalizedSeries == null) return;

        var normalizedNonLocalizedSeries = Parser.Parser.Normalize(nonLocalizedSeries);

        // Update infos that need mapping
        foreach (var infoNeedingMapping in infos.Where(i => !Parser.Parser.Normalize(i.Series).Equals(normalizedNonLocalizedSeries)))
        {
            infoNeedingMapping.Series = nonLocalizedSeries;
            infoNeedingMapping.LocalizedSeries = localizedSeries;
        }
    }

}
