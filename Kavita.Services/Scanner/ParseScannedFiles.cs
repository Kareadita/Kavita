using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;
using Kavita.Services.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Responsible for taking parsed info from ReadingItemService and DirectoryService and combining them to emit DB work
/// on a series by series.
/// </summary>
public partial class ParseScannedFiles
{
    private readonly ILogger _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IEventHub _eventHub;
    private readonly IMediaErrorService _mediaErrorService;

    /// <summary>
    /// File id to the write time from the listing, for files in unchanged folders that have none stored yet
    /// </summary>
    /// <remarks>v0.9.2 added this, after one full stable, this can be removed as one scan will backfill times</remarks>
    public Dictionary<int, DateTime> WriteTimesToBackfill { get; } = new();

    private readonly Dictionary<string, ScanIssue> _issues = new();
    private readonly HashSet<string> _failedFilesInReadFolders = [];

    /// <summary>
    /// Issues with files that were read this scan, at most one per path
    /// </summary>
    public IReadOnlyCollection<ScanIssue> Issues => _issues.Values;

    /// <summary>
    /// Earlier failures in folders that were read this scan, whose file is gone or has no issue anymore
    /// </summary>
    public IEnumerable<string> ResolvedFailedFiles => _failedFilesInReadFolders.Where(path => !_issues.ContainsKey(path));

    /// <summary>
    /// An instance of a pipeline for processing files and returning a Map of Series -> ParserInfos.
    /// Each instance is separate from other threads, allowing for no cross over.
    /// </summary>
    /// <param name="logger">Logger of the parent class that invokes this</param>
    /// <param name="directoryService">Directory Service</param>
    /// <param name="readingItemService">ReadingItemService Service for extracting information on a number of formats</param>
    /// <param name="eventHub">For firing off SignalR events</param>
    /// <param name="mediaErrorService"></param>
    public ParseScannedFiles(ILogger logger, IDirectoryService directoryService,
        IReadingItemService readingItemService, IEventHub eventHub, IMediaErrorService mediaErrorService)
    {
        _logger = logger;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _eventHub = eventHub;
        _mediaErrorService = mediaErrorService;
    }

    /// <summary>
    /// This will Scan all files in a folder path. For each folder within the folderPath, FolderAction will be invoked for all files contained
    /// </summary>
    /// <param name="scanDirectoryByDirectory">Scan directory by directory and for each, call folderAction</param>
    /// <param name="seriesPaths">A dictionary mapping a normalized path to a list of <see cref="SeriesModified"/> to help scanner skip I/O</param>
    /// <param name="folderPath">A library folder or series folder</param>
    /// <param name="forceCheck">If we should bypass any folder last write time checks on the scan and force I/O</param>
    /// <param name="failedFiles">Files that failed on an earlier scan, known while unchanged. Not known with <paramref name="forceCheck"/></param>
    public async Task<IList<ScanResult>> ScanFiles(string folderPath, bool scanDirectoryByDirectory,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Library library, bool forceCheck = false,
        IReadOnlyList<FailedFile>? failedFiles = null)
    {
        var fileExtensions = string.Join("|", library.LibraryFileTypes.Select(l => l.FileTypeGroup.GetRegex()));

        // If there are no library file types, skip scanning entirely
        if (string.IsNullOrWhiteSpace(fileExtensions))
        {
            return ArraySegment<ScanResult>.Empty;
        }

        var matcher = BuildMatcher(library);
        var failures = failedFiles ?? [];

        var result = new List<ScanResult>();

        // Not to self: this whole thing can be parallelized because we don't deal with any DB or global state
        if (scanDirectoryByDirectory)
        {
            return await ScanDirectories(folderPath, seriesPaths, failures, library, forceCheck, matcher, result, fileExtensions);
        }

        return await ScanSingleDirectory(folderPath, seriesPaths, failures, library, forceCheck, result, fileExtensions, matcher);
    }

    private async Task<IList<ScanResult>> ScanDirectories(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths,
        IReadOnlyList<FailedFile> failedFiles, Library library, bool forceCheck, GlobMatcher matcher, List<ScanResult> result, string fileExtensions)
    {
        var allDirectories = _directoryService.GetAllDirectories(folderPath, matcher)
            .Select(Parser.NormalizePath)
            .OrderByDescending(d => d.Length)
            .ToList();

        var processedDirs = new HashSet<string>();
        var skippedSpecials = new List<string>();
        var total = allDirectories.Count;
        var timings = new DirectoryScanTimings();
        var loopSw = Stopwatch.StartNew();

        _logger.LogDebug("[ScannerService] Step 1.C Found {DirectoryCount} directories to process for {FolderPath}", total, folderPath);
        for (var i = 0; i < total; i++)
        {
            var directory = allDirectories[i];

            timings.Events.Start();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(directory, library.Id, library.Name, ProgressEventType.Updated,
                    MessageEventCode.ScanListingFolders, i + 1, total));
            timings.Events.Stop();

            var isParent = processedDirs.Any(d => d.StartsWith(directory + Path.AltDirectorySeparatorChar) || d.Equals(directory));
            try
            {
                // Subfolders were already read, so only the loose files at this level and its Specials folders are left
                if (isParent)
                {
                    timings.ParentCount++;
                    timings.ParentChangeCheck.Start();

                    var specials = skippedSpecials.Where(s => IsDirectChild(s, directory)).ToList();
                    var looseFileOwners = forceCheck ? [] : SeriesWithFilesIn(seriesPaths, directory);
                    var specialsOwners = specials.Select(s => forceCheck ? [] : SeriesWithFilesIn(seriesPaths, s)).ToList();
                    var allOwners = looseFileOwners.Concat(specialsOwners.SelectMany(o => o)).Distinct().ToList();

                    var onDisk = ListLibraryFiles(directory, fileExtensions, matcher, library.Type, SearchOption.TopDirectoryOnly)
                        .Concat(specials.SelectMany(s => ListLibraryFiles(s, fileExtensions, matcher, library.Type)))
                        .ToList();

                    Func<string, bool> isInScope = folder => folder == directory || specials.Exists(folder.IsSameOrInsideFolder);
                    var failures = FailedFilesIn(failedFiles, isInScope);
                    var unchanged = !forceCheck && (allOwners.Count > 0 || failures.Count > 0) &&
                                    FolderChangeCheck.IsUnchanged(onDisk, allOwners, failures, isInScope, WriteTimesToBackfill);

                    timings.ParentChangeCheck.Stop();
                    timings.ParentSurfaceFiles.Start();

                    if (unchanged)
                    {
                        if (looseFileOwners.Count > 0)
                        {
                            HandleUnchangedFolder(result, folderPath, directory, looseFileOwners, true);
                        }

                        for (var j = 0; j < specials.Count; j++)
                        {
                            if (specialsOwners[j].Count == 0) continue;
                            HandleUnchangedFolder(result, folderPath, specials[j], specialsOwners[j], false);
                        }
                    }
                    else
                    {
                        MarkRead(failures);
                        AddSurfaceAndSpecialsFiles(result, directory, folderPath, onDisk);
                    }
                    timings.ParentSurfaceFiles.Stop();
                    continue;
                }

                // Skip directories ending with "Specials", let the parent handle it
                if (directory.EndsWith("Specials", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Skipping {Directory} as it ends with 'Specials'", directory);
                    skippedSpecials.Add(directory);
                    continue;
                }

                // A full scan also reads the Specials folders skipped below this one
                var owners = forceCheck
                    ? []
                    : skippedSpecials
                        .Where(s => s.IsInsideFolder(directory))
                        .Prepend(directory)
                        .SelectMany(f => SeriesWithFilesIn(seriesPaths, f))
                        .Distinct()
                        .ToList();

                var onDiskBelow = ListLibraryFiles(directory, fileExtensions, matcher, library.Type);
                Func<string, bool> isBelow = folder => folder.IsSameOrInsideFolder(directory);
                var failuresBelow = FailedFilesIn(failedFiles, isBelow);

                if (!forceCheck && (owners.Count > 0 || failuresBelow.Count > 0) &&
                    FolderChangeCheck.IsUnchanged(onDiskBelow, owners, failuresBelow, isBelow, WriteTimesToBackfill))
                {
                    HandleUnchangedFolder(result, folderPath, directory, owners, false);
                }
                else
                {
                    MarkRead(failuresBelow);
                    AddChangedFolder(result, directory, folderPath, onDiskBelow);
                }

                processedDirs.Add(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                timings.ParentChangeCheck.Stop();
                timings.ParentSurfaceFiles.Stop();
                _logger.LogWarning(ex, "[ScannerService] Could not read {Directory}, keeping its series as they are for this scan", directory);
                KeepUnreadableFolder(result, folderPath, directory, isParent, skippedSpecials, seriesPaths);
                if (!isParent) processedDirs.Add(directory);
            }
        }


        _logger.LogDebug("[ScannerService] Walked {DirectoryCount} folders in {ElapsedMs}ms, {ParentCount} parent checks took {ChangeCheckMs}ms + {SurfaceFilesMs}ms loose files, events {EventsMs}ms",
            total, loopSw.ElapsedMilliseconds, timings.ParentCount,
            timings.ParentChangeCheck.ElapsedMilliseconds, timings.ParentSurfaceFiles.ElapsedMilliseconds,
            timings.Events.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Placeholders for every series with files in a folder that could not be read, so none of them are removed (due to lack of forced check).
    /// A parent's subfolders were already read, so it only covers its own loose files and its Specials folders
    /// </summary>
    private void KeepUnreadableFolder(List<ScanResult> result, string folderPath, string directory, bool isParent,
        IEnumerable<string> skippedSpecials, IDictionary<string, IList<SeriesModified>> seriesPaths)
    {
        if (!isParent)
        {
            var owners = SeriesWithFilesInside(seriesPaths, directory);
            if (owners.Count > 0)
            {
                HandleUnchangedFolder(result, folderPath, directory, owners, false);
            }

            return;
        }

        var looseFileOwners = SeriesWithFilesIn(seriesPaths, directory);
        if (looseFileOwners.Count > 0) HandleUnchangedFolder(result, folderPath, directory, looseFileOwners, true);

        foreach (var special in skippedSpecials.Where(s => IsDirectChild(s, directory)))
        {
            var specialOwners = SeriesWithFilesIn(seriesPaths, special);
            if (specialOwners.Count > 0)
            {
                HandleUnchangedFolder(result, folderPath, special, specialOwners, false);
            }
        }
    }

    private sealed class DirectoryScanTimings
    {
        public int ParentCount;
        public readonly Stopwatch ParentChangeCheck = new();
        public readonly Stopwatch ParentSurfaceFiles = new();
        public readonly Stopwatch Events = new();
    }

    private static List<FailedFile> FailedFilesIn(IReadOnlyList<FailedFile> failedFiles, Func<string, bool> isInScope)
    {
        return failedFiles.Count == 0 ? [] : failedFiles.Where(f => isInScope(f.Path.FolderOf())).ToList();
    }

    private void MarkRead(IEnumerable<FailedFile> failedFiles)
    {
        _failedFilesInReadFolders.UnionWith(failedFiles.Select(f => f.Path));
    }

    private static List<SeriesModified> SeriesWithFilesIn(IDictionary<string, IList<SeriesModified>> seriesPaths, string folder)
    {
        return seriesPaths.TryGetValue(folder, out var seriesList)
            ? seriesList.Where(s => s.FilesByFolder.ContainsKey(folder)).ToList()
            : [];
    }

    /// <summary>
    /// Files the parsers would turn into series files. Anything else would make the folder look changed on every scan
    /// </summary>
    private List<FileStamp> ListLibraryFiles(string directory, string fileExtensions, GlobMatcher matcher, LibraryType type,
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        return _directoryService.ScanFiles(directory, fileExtensions, matcher, searchOption)
            .Where(f => !Parser.IsSkippedCoverImage(Path.GetFileName(f.Path), type))
            .ToList();
    }

    private static List<SeriesModified> SeriesWithFilesInside(IDictionary<string, IList<SeriesModified>> seriesPaths, string folder)
    {
        return seriesPaths.Values
            .SelectMany(s => s)
            .Distinct()
            .Where(s => s.FilesByFolder.Keys.Any(f => f.IsSameOrInsideFolder(folder)))
            .ToList();
    }

    /// <param name="directory">This should be normalized</param>
    private static bool CanCompareSeriesFolder(IDictionary<string, IList<SeriesModified>> seriesPaths, string directory, bool forceCheck)
    {
        if (forceCheck || !seriesPaths.TryGetValue(directory, out var seriesList))
        {
            return false;
        }

        // Null stays "changed" (FolderPath alone misses a deleted sibling folder)
        return seriesList.All(series => !string.IsNullOrEmpty(series.LowestFolderPath) &&
                                        series.LibraryRoots.Any(series.LowestFolderPath.IsInsideFolder));
    }

    /// <summary>
    /// Handles directories that haven't changed since the last scan.
    /// </summary>
    private void HandleUnchangedFolder(List<ScanResult> result, string folderPath, string directory,
        IList<SeriesModified> owners, bool isShallow)
    {
        if (result.Exists(r => r.Folder == directory))
        {
            _logger.LogDebug("[ProcessFiles] Skipping adding {Directory} as it's already added, this indicates a bad layout issue", directory);
        }
        else
        {
            _logger.LogDebug("[ProcessFiles] Skipping {Directory} as it hasn't changed since last scan", directory);
            var unchanged = CreateScanResult(directory, folderPath, false, ArraySegment<FileStamp>.Empty);
            unchanged.UnchangedSeries = owners;
            unchanged.IsShallow = isShallow;
            result.Add(unchanged);
        }
    }

    private void AddChangedFolder(List<ScanResult> result, string directory, string folderPath, List<FileStamp> files)
    {
        _logger.LogDebug("[ProcessFiles] Performing full scan on {Directory}", directory);
        if (files.Count == 0)
        {
            _logger.LogDebug("[ProcessFiles] Empty directory: {Directory}. Keeping empty will cause Kavita to scan this each time", directory);
        }
        result.Add(CreateScanResult(directory, folderPath, true, files));
    }

    /// <summary>
    /// Reads the files directly in the directory and everything in the given Specials folders.
    /// They share one result so the files are parsed with the directory as root, else the series would be named "Specials"
    /// </summary>
    private static void AddSurfaceAndSpecialsFiles(List<ScanResult> result, string directory, string folderPath,
        List<FileStamp> files)
    {
        if (files.Count == 0)
        {
            return;
        }
        result.Add(CreateScanResult(directory, folderPath, true, files));
    }

    private static bool IsDirectChild(string folder, string parent)
    {
        var lastSlash = folder.LastIndexOf('/');
        return lastSlash > 0 && folder.AsSpan(0, lastSlash).Equals(parent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Scans a single directory and processes the scan result.
    /// </summary>
    private async Task<IList<ScanResult>> ScanSingleDirectory(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths,
        IReadOnlyList<FailedFile> failedFiles, Library library, bool forceCheck, List<ScanResult> result,
        string fileExtensions, GlobMatcher matcher)
    {
        var normalizedPath = Parser.NormalizePath(folderPath);
        var libraryRoot =
            library.Folders.FirstOrDefault(f => normalizedPath.IsSameOrInsideFolder(f.Path))?.Path ??
            folderPath;

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent(normalizedPath, library.Id, library.Name, ProgressEventType.Updated,
                MessageEventCode.ScanListingFolders, 1, 1));

        var onDisk = ListLibraryFiles(folderPath, fileExtensions, matcher, library.Type);

        // Every series with files inside, so a folder shared by several series does not see the others' files as new
        var canCompare = CanCompareSeriesFolder(seriesPaths, normalizedPath, forceCheck);
        var owners = canCompare ? SeriesWithFilesInside(seriesPaths, normalizedPath) : [];

        Func<string, bool> isInside = folder => folder.IsSameOrInsideFolder(normalizedPath);
        var failures = FailedFilesIn(failedFiles, isInside);
        var knownFailures = canCompare ? failures : [];

        if ((owners.Count > 0 || knownFailures.Count > 0) &&
            FolderChangeCheck.IsUnchanged(onDisk, owners, knownFailures, isInside, WriteTimesToBackfill))
        {
            var unchanged = CreateScanResult(folderPath, libraryRoot, false, ArraySegment<FileStamp>.Empty);
            unchanged.UnchangedSeries = seriesPaths[normalizedPath];
            result.Add(unchanged);
        }
        else
        {
            MarkRead(failures);
            result.Add(CreateScanResult(folderPath, libraryRoot, true, onDisk));
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
        IList<FileStamp> files)
    {
        return new ScanResult()
        {
            Files = files,
            Folder = Parser.NormalizePath(folderPath),
            LibraryRoot = libraryRoot,
            HasChanged = hasChanged
        };
    }

    /// <summary>
    /// Processes scanResults to track all series across the combined results.
    /// Ensures series are correctly grouped even if they span multiple folders.
    /// </summary>
    /// <param name="scanResults">A collection of scan results</param>
    /// <param name="scannedSeries">A concurrent dictionary to store the tracked series</param>
    /// <returns>Files that were turned away</returns>
    public IList<(ParserInfo Info, ParseIssue Issue)> TrackSeriesAcrossScanResults(IList<ScanResult> scanResults, ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries)
    {
        // Flatten all ParserInfos from scanResults
        var allInfos = scanResults.SelectMany(sr => sr.ParserInfos).ToList();
        var rejected = new List<(ParserInfo, ParseIssue)>();

        // Iterate through each ParserInfo and track the series
        foreach (var info in allInfos)
        {
            if (info == null) continue;

            try
            {
                var rejection = TrackSeries(scannedSeries, info);
                if (rejection != null && !string.IsNullOrEmpty(info.FullFilePath)) rejected.Add((info, rejection));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ScannerService] Exception occurred during tracking {FilePath}. Skipping this file", info?.FullFilePath);
            }
        }

        return rejected;
    }


    /// <summary>
    /// Attempts to either add a new instance of a series mapping to the _scannedSeries bag or adds to an existing.
    /// This will check if the name matches an existing series name (multiple fields) <see cref="MergeName"/>
    /// </summary>
    /// <param name="scannedSeries">A localized list of a series' parsed infos</param>
    /// <param name="info"></param>
    /// <returns>Set when the file is turned away</returns>
    private ParseIssue? TrackSeries(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParserInfo? info)
    {
        if (info == null) return null;
        if (info.Series == string.Empty) return ParseIssues.FromFailedParse(info);

        // Do not ingest series with no meaningful information as title. These break merging as they'll all merge into each other
        // They would also merge all series that don't have localized names previously
        // This does create the edge case where some series may really not have any meaningful information.
        // But until this is reported, we should ignore it and play it safe!
        if (string.IsNullOrEmpty(info.Series.ToNormalized()))
        {
            _logger.LogCritical("[ScannerService] {SeriesName} @ {FileName} is empty when normalized, this file will not be ingested! The filename does not follow our guidelines or this is a bug in the parser, please report this! https://github.com/Kareadita/Kavita/issues",
                info.Series, info.Filename);

            return new ParseIssue(MediaErrorReason.NoSeriesName, "Failed to parse a valid series name for a file",
                $"{info.Series} is empty when normalized, this file will not be ingested! The filename does not follow our guidelines or this is a bug in the parser, please report this! https://github.com/Kareadita/Kavita/issues");
        }

        // Check if normalized info.Series already exists and if so, update info to use that name instead
        info.Series = MergeName(scannedSeries, info);

        // BUG: This will fail for Solo Leveling & Solo Leveling (Manga)

        var normalizedSeries = info.Series.ToNormalized();
        var normalizedSortSeries = info.SeriesSort.ToNormalized();
        var normalizedLocalizedSeries = info.LocalizedSeries.ToNormalized();

        try
        {
            var existingKey = scannedSeries.Keys.SingleOrDefault(Guard);
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
            #pragma warning disable S6667
            _logger.LogCritical("[ScannerService] {SeriesName} matches against multiple series in the parsed series. This indicates a critical unsupported layout issue. Key will be skipped", info.Series);
            #pragma warning restore S6667
            foreach (var seriesKey in scannedSeries.Keys.Where(Guard))
            {
                _logger.LogCritical("[ScannerService] Matches: '{SeriesName}' matches on '{SeriesKey}'", info.Series, seriesKey.Name);
            }
        }

        return null;

        bool Guard(ParsedSeries series)
        {
            return MergeNameGuard(series.Format, series.NormalizedName, info.Format,
                normalizedSeries, normalizedSortSeries, normalizedLocalizedSeries);
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

        var normalizedName = info.Series.ToNormalized();
        var normalizedSortName = info.SeriesSort.ToNormalized();
        var normalizedLocalizedName = info.LocalizedSeries.ToNormalized();

        try
        {
            var existingName = scannedSeries.SingleOrDefault(Guard).Key;

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
            #pragma warning disable S6667
            _logger.LogCritical("[ScannerService] Multiple series detected for {SeriesName} ({File})! This is critical to fix! There should only be 1", info.Series, info.FullFilePath);
            #pragma warning restore S6667
            var values = scannedSeries.Where(Guard);

            foreach (var pair in values)
            {
                _logger.LogCritical("[ScannerService] Duplicate Series in DB matches with {SeriesName}: {DuplicateName}", info.Series, pair.Key.Name);
            }

        }

        return info.Series;

        bool Guard(KeyValuePair<ParsedSeries, List<ParserInfo>> p)
        {
            return MergeNameGuard(p.Key.Format, p.Key.NormalizedName, info.Format,
                normalizedName, normalizedSortName, normalizedLocalizedName);
        }
    }

    /// <summary>
    /// Checks if the given series should be merged into the other
    /// </summary>
    /// <param name="mergeIntoFormat"></param>
    /// <param name="mergeIntoSeries">Normalised name of the series to be merged into</param>
    /// <param name="format"></param>
    /// <param name="normalizedNames"></param>
    /// <returns></returns>
    private static bool MergeNameGuard(
        MangaFormat mergeIntoFormat, string mergeIntoSeries,
        MangaFormat format, params string[] normalizedNames)
    {
        if (mergeIntoFormat != format) return false;

        if (string.IsNullOrEmpty(mergeIntoSeries)) return false;

        return normalizedNames.Any(n => n.Equals(mergeIntoSeries));
    }

    /// <summary>
    /// This will process series by folder groups. This is used by ScanSeries and ScanLibrary
    /// </summary>
    /// <param name="library">This should have the FileTypes included</param>
    /// <param name="folders"></param>
    /// <param name="isLibraryScan">If true, does a directory scan first (resulting in folders being tackled in parallel), else does an immediate scan files</param>
    /// <param name="seriesPaths">A map of Series names -> existing folder paths to handle skipping folders</param>
    /// <param name="forceCheck">Defaults to false</param>
    /// <param name="failedFiles">Files that failed on an earlier scan, known while unchanged</param>
    /// <returns></returns>
    public async Task<IList<ScannedSeriesResult>> ScanLibrariesForSeries(Library library,
        IList<string> folders, bool isLibraryScan,
        IDictionary<string, IList<SeriesModified>> seriesPaths, bool forceCheck = false,
        IReadOnlyList<FailedFile>? failedFiles = null)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent("File Scan Starting", library.Id, library.Name, ProgressEventType.Started));

        _logger.LogDebug("[ScannerService] Library {LibraryName} Step 1.A: Process {FolderCount} folders", library.Name, folders.Count);
        var processedScannedSeries = new ConcurrentBag<ScannedSeriesResult>();
        var unchangedFolders = new List<ScanResult>();
        var placeholderOwners = new Dictionary<ParserInfo, SeriesModified>(ReferenceEqualityComparer.Instance);

        foreach (var folder in folders)
        {
            try
            {
                var scanResults = await ScanAndParseFolder(folder, library, isLibraryScan, seriesPaths,
                    processedScannedSeries, placeholderOwners, forceCheck, failedFiles);
                unchangedFolders.AddRange(scanResults.Where(r => !r.HasChanged));
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folder);
            }
        }

        FlagSeriesWithMissingFiles(processedScannedSeries, folders, seriesPaths, unchangedFolders, placeholderOwners);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent("File Scan Done", library.Id, library.Name, ProgressEventType.Ended));

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
    /// <param name="placeholderOwners">Filled with the series each placeholder was made for</param>
    /// <param name="forceCheck"></param>
    /// <returns>Every folder result of the walk</returns>
    private async Task<IList<ScanResult>> ScanAndParseFolder(string folderPath, Library library,
        bool isLibraryScan, IDictionary<string, IList<SeriesModified>> seriesPaths,
        ConcurrentBag<ScannedSeriesResult> processedScannedSeries,
        Dictionary<ParserInfo, SeriesModified> placeholderOwners, bool forceCheck, IReadOnlyList<FailedFile>? failedFiles)
    {
        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.B: Scan files in {Folder}", library.Name, folderPath);
        var scanResults = await ScanFiles(folderPath, isLibraryScan, seriesPaths, library, forceCheck, failedFiles);
        var walkResults = scanResults;

        // Aggregate the scanned series across all scanResults
        var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.C: Process files in {Folder}", library.Name, folderPath);
        for (var i = 0; i < scanResults.Count; i++)
        {
            await ParseFiles(scanResults[i], placeholderOwners, library, i + 1, scanResults.Count);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent(folderPath, library.Id, library.Name, ProgressEventType.Updated,
                MessageEventCode.ScanGroupingSeries));

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.D: Merge any localized series with series {Folder}", library.Name, folderPath);
        scanResults = MergeLocalizedSeriesAcrossScanResults(scanResults);

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.E: Group all parsed data into logical Series", library.Name);
        var rejected = TrackSeriesAcrossScanResults(scanResults, scannedSeries);
        AddRejectedIssues(walkResults, rejected);


        // Now transform and add to processedScannedSeries AFTER everything is processed
        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.F: Generate Sort Order for Series and Finalize", library.Name);
        GenerateProcessedScannedSeries(scannedSeries, processedScannedSeries);

        return walkResults;
    }

    /// <summary>
    /// Records the files <see cref="TrackSeries"/> turned away, with the stamp from the listing that read them.
    /// This replaces a metadata issue recorded for the same file while parsing
    /// </summary>
    private void AddRejectedIssues(IList<ScanResult> walkResults, IList<(ParserInfo Info, ParseIssue Issue)> rejected)
    {
        if (rejected.Count == 0) return;

        var stamps = new Dictionary<string, FileStamp>();
        foreach (var stamp in walkResults.Where(r => r.HasChanged).SelectMany(r => r.Files))
        {
            stamps.TryAdd(Parser.NormalizePath(stamp.Path), stamp);
        }

        foreach (var (info, issue) in rejected)
        {
            if (!stamps.TryGetValue(info.FullFilePath, out var stamp)) continue;
            AddIssue(stamp, issue);
        }
    }

    private void AddIssue(FileStamp stamp, ParseIssue issue)
    {
        var scanIssue = ScanIssue.From(stamp, issue);
        _issues[scanIssue.Path] = scanIssue;
    }

    /// <summary>
    /// A series whose known files sit in a folder that was neither read nor skipped as unchanged lost files there
    /// (folder deleted or emptied). It has only placeholders, so it is marked to be processed anyway
    /// </summary>
    private static void FlagSeriesWithMissingFiles(IEnumerable<ScannedSeriesResult> processedScannedSeries,
        IList<string> scannedFolders, IDictionary<string, IList<SeriesModified>> seriesPaths,
        IList<ScanResult> unchangedFolders, Dictionary<ParserInfo, SeriesModified> placeholderOwners)
    {
        if (placeholderOwners.Count == 0) return;

        var roots = scannedFolders.Select(Parser.NormalizePath).ToList();
        var shallow = unchangedFolders.Where(r => r.IsShallow).Select(r => r.Folder.TrimEnd('/')).ToHashSet();
        var recursive = unchangedFolders.Where(r => !r.IsShallow).Select(r => r.Folder.TrimEnd('/')).ToHashSet();

        var seriesWithMissingFiles = seriesPaths.Values
            .SelectMany(s => s)
            .Distinct()
            .Where(s => s.FilesByFolder.Keys.Any(f => roots.Exists(f.IsSameOrInsideFolder) && !IsCovered(f)))
            .ToHashSet();

        if (seriesWithMissingFiles.Count == 0) return;

        foreach (var result in processedScannedSeries)
        {
            var hasMissingFiles = result.ParsedInfos.Any(info =>
                placeholderOwners.TryGetValue(info, out var owner) && seriesWithMissingFiles.Contains(owner));
            if (!hasMissingFiles) continue;

            result.HasMissingFiles = true;
            result.HasChanged = true;
        }

        return;

        bool IsCovered(string folder)
        {
            if (shallow.Contains(folder)) return true;

            for (var current = folder; !string.IsNullOrEmpty(current); current = ParentOf(current))
            {
                if (recursive.Contains(current)) return true;
            }

            return false;
        }

        static string ParentOf(string folder)
        {
            var lastSlash = folder.LastIndexOf('/');
            return lastSlash <= 0 ? string.Empty : folder[..lastSlash];
        }
    }

    /// <summary>
    /// Processes and generates the final results for processedScannedSeries after updating sort order.
    /// </summary>
    /// <param name="scannedSeries">A concurrent dictionary of tracked series and their parsed infos</param>
    /// <param name="processedScannedSeries">A thread-safe concurrent bag of processed series results</param>
    private void GenerateProcessedScannedSeries(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ConcurrentBag<ScannedSeriesResult> processedScannedSeries)
    {
        // First, update the sort order for all series
        UpdateSeriesSortOrder(scannedSeries);

        // Now, generate the final processed scanned series results
        CreateFinalSeriesResults(scannedSeries, processedScannedSeries);
    }

    /// <summary>
    /// Updates the sort order for all series in the scannedSeries dictionary.
    /// </summary>
    /// <param name="scannedSeries">A concurrent dictionary of tracked series and their parsed infos</param>
    private void UpdateSeriesSortOrder(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries)
    {
        foreach (var series in scannedSeries.Keys)
        {
            if (scannedSeries[series].Count <= 0) continue;

            try
            {
                UpdateSortOrder(scannedSeries, series);  // Call to method that updates sort order
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ScannerService] Issue occurred while setting IssueOrder for series {SeriesName}", series.Name);
            }
        }
    }

    /// <summary>
    /// Generates the final processed scanned series results after processing the sort order.
    /// </summary>
    /// <param name="scannedSeries">A concurrent dictionary of tracked series and their parsed infos</param>
    /// <param name="processedScannedSeries">The list where processed results will be added</param>
    private static void CreateFinalSeriesResults(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries,
        ConcurrentBag<ScannedSeriesResult> processedScannedSeries)
    {
        foreach (var series in scannedSeries.Keys)
        {
            if (scannedSeries[series].Count <= 0) continue;

            processedScannedSeries.Add(new ScannedSeriesResult
            {
                HasChanged = scannedSeries[series].Exists(info => !string.IsNullOrEmpty(info.Filename)),
                ParsedSeries = series,
                ParsedInfos = scannedSeries[series]
            });
        }
    }

    /// <summary>
    /// Merges localized series with the series field across all scan results.
    /// Combines ParserInfos from all scanResults and processes them collectively
    /// to ensure consistent series names.
    /// </summary>
    /// <example>
    /// Accel World v01.cbz has Series "Accel World" and Localized Series "World of Acceleration"
    /// World of Acceleration v02.cbz has Series "World of Acceleration"
    /// After running this code, we'd have:
    /// World of Acceleration v02.cbz having Series "Accel World" and Localized Series of "World of Acceleration"
    /// </example>
    /// <param name="scanResults">A collection of scan results</param>
    /// <returns>A new list of scan results with merged series</returns>
    private IList<ScanResult> MergeLocalizedSeriesAcrossScanResults(IList<ScanResult> scanResults)
    {
        // Flatten all ParserInfos across scanResults
        var allInfos = scanResults.SelectMany(sr => sr.ParserInfos).ToList();

        // Filter relevant infos (non-special and with localized series)
        var relevantInfos = GetRelevantInfos(allInfos);

        if (relevantInfos.Count == 0) return scanResults;

        // Get distinct localized series and process each one
        var distinctLocalizedSeries = relevantInfos
            .Select(i => i.LocalizedSeries)
            .Distinct()
            .ToList();

        foreach (var localizedSeries in distinctLocalizedSeries)
        {
            if (string.IsNullOrEmpty(localizedSeries)) continue;

            // Process the localized series for merging
            ProcessLocalizedSeries(scanResults, allInfos, relevantInfos, localizedSeries);
        }

        // Remove or clear any scan results that now have no ParserInfos after merging
        return scanResults.Where(sr => sr.ParserInfos.Count > 0).ToList();
    }

    private static List<ParserInfo> GetRelevantInfos(List<ParserInfo> allInfos)
    {
        return allInfos
            .Where(i => !i.IsSpecial && !string.IsNullOrEmpty(i.LocalizedSeries))
            .GroupBy(i => i.Format)
            .SelectMany(g => g.ToList())
            .ToList();
    }

    private void ProcessLocalizedSeries(IList<ScanResult> scanResults, List<ParserInfo> allInfos, List<ParserInfo> relevantInfos, string localizedSeries)
    {
        var seriesForLocalized = GetSeriesForLocalized(relevantInfos, localizedSeries);
        if (seriesForLocalized.Count == 0) return;

        var nonLocalizedSeries = GetNonLocalizedSeries(seriesForLocalized, localizedSeries);
        if (nonLocalizedSeries == null) return;

        // Remap and update relevant ParserInfos
        RemapSeries(scanResults, allInfos, localizedSeries, nonLocalizedSeries);

    }

    private static List<string> GetSeriesForLocalized(List<ParserInfo> relevantInfos, string localizedSeries)
    {
        return relevantInfos
            .Where(i => i.LocalizedSeries == localizedSeries)
            .DistinctBy(r => r.Series)
            .Select(r => r.Series)
            .ToList();
    }

    private string? GetNonLocalizedSeries(List<string> seriesForLocalized, string localizedSeries)
    {
        switch (seriesForLocalized.Count)
        {
            case 1:
                return seriesForLocalized[0];
            case <= 2:
                return seriesForLocalized.FirstOrDefault(s => !s.Equals(Parser.Normalize(localizedSeries)));
            default:
                _logger.LogError(
                    "[ScannerService] Multiple series detected across scan results that contain localized series. " +
                    "This will cause them to group incorrectly. Please separate series into their own dedicated folder: {LocalizedSeries}",
                    string.Join(", ", seriesForLocalized)
                );
                return null;
        }
    }

    private static void RemapSeries(IList<ScanResult> scanResults, List<ParserInfo> allInfos, string localizedSeries, string nonLocalizedSeries)
    {
        // If the series names are identical, no remapping is needed (rare but valid)
        if (localizedSeries.ToNormalized().Equals(nonLocalizedSeries.ToNormalized()))
        {
            return;
        }

        // Find all infos that need to be remapped from the localized series to the non-localized series
        var normalizedLocalizedSeries = localizedSeries.ToNormalized();
        var seriesToBeRemapped = allInfos.Where(i => i.Series.ToNormalized().Equals(normalizedLocalizedSeries)).ToList();

        foreach (var infoNeedingMapping in seriesToBeRemapped)
        {
            infoNeedingMapping.Series = nonLocalizedSeries;

            // Find the scan result containing the localized info
            var localizedScanResult = scanResults.FirstOrDefault(sr => sr.ParserInfos.Contains(infoNeedingMapping));
            if (localizedScanResult == null) continue;

            // Remove the localized series from this scan result
            localizedScanResult.ParserInfos.Remove(infoNeedingMapping);

            // Find the scan result that should be merged with
            var nonLocalizedScanResult = scanResults.FirstOrDefault(sr => sr.ParserInfos.Any(pi => pi.Series == nonLocalizedSeries));

            if (nonLocalizedScanResult == null) continue;

            // Add the remapped info to the non-localized scan result
            nonLocalizedScanResult.ParserInfos.Add(infoNeedingMapping);

            // Assign the higher folder path (i.e., the one closer to the root)
            //nonLocalizedScanResult.Folder = DirectoryService.GetDeepestCommonPath(localizedScanResult.Folder, nonLocalizedScanResult.Folder);
        }
    }

    /// <summary>
    /// For a given ScanResult, sets the ParserInfos on the result
    /// </summary>
    /// <param name="result"></param>
    /// <param name="placeholderOwners"></param>
    /// <param name="library"></param>
    private async Task ParseFiles(ScanResult result, Dictionary<ParserInfo, SeriesModified> placeholderOwners, Library library,
        int current, int total)
    {
        var normalizedFolder = Parser.NormalizePath(result.Folder);

        // If folder hasn't changed, generate fake ParserInfos
        if (!result.HasChanged)
        {
            result.ParserInfos = result.UnchangedSeries
                .Select(fp =>
                {
                    var placeholder = new ParserInfo
                    {
                        Series = fp.SeriesName,
                        Format = fp.Format,
                        UnchangedFolderPath = normalizedFolder,
                        UnchangedFolderIsShallow = result.IsShallow,
                    };
                    placeholderOwners[placeholder] = fp;
                    return placeholder;
                })
                .ToList();

            _logger.LogDebug("[ScannerService] Skipped File Scan for {Folder} as it hasn't changed", normalizedFolder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent($"Skipped {normalizedFolder}", library.Id, library.Name, ProgressEventType.Updated,
                    MessageEventCode.ScanReadingFiles, current, total));
            return;
        }

        var files = result.Files;
        var fileCount = files.Count;

        if (fileCount == 0)
        {
            _logger.LogInformation("[ScannerService] {Folder} is empty or has no matching file types", normalizedFolder);
            result.ParserInfos = ArraySegment<ParserInfo>.Empty;
            return;
        }

        _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, normalizedFolder);
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent($"{fileCount} files in {normalizedFolder}", library.Id, library.Name, ProgressEventType.Updated,
                MessageEventCode.ScanReadingFiles, current, total));

        // Written by index rather than collected, as downstream series mapping relies on file order
        var parsed = new ParseFileResult[fileCount];

        if (fileCount < 100)
        {
            for (var i = 0; i < fileCount; i++)
            {
                parsed[i] = _readingItemService.ParseFile(files[i].Path, normalizedFolder, result.LibraryRoot, library.Type, library.EnableMetadata);
            }
        }
        else
        {
            // Process files in parallel, but bound the concurrency so that a folder with
            // many files cannot flood the ThreadPool and starve request handling (the web UI
            // and health checks become unresponsive during scans otherwise).
            // Matches the scanner's existing parallelism convention (see ScannerService).
            var maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);

            await Parallel.ForEachAsync(Enumerable.Range(0, fileCount),
                new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency },
                (i, _) =>
                {
                    parsed[i] = _readingItemService.ParseFile(files[i].Path, normalizedFolder, result.LibraryRoot,
                        library.Type, library.EnableMetadata);
                    return ValueTask.CompletedTask;
                });
        }

        var infos = new List<ParserInfo>(fileCount);
        for (var i = 0; i < fileCount; i++)
        {
            if (parsed[i].Info != null) infos.Add(parsed[i].Info!);
            if (parsed[i].Issue != null) AddIssue(files[i], parsed[i].Issue!);
        }

        result.ParserInfos = infos;
    }


    public static void UpdateSortOrder(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParsedSeries series)
    {
        // Placeholders from skipped folders don't map to a chapter, so they have nothing to sort
        var fileInfos = scannedSeries[series].Where(info => string.IsNullOrEmpty(info.UnchangedFolderPath)).ToList();

        // Set the Sort order per Volume
        var volumes = fileInfos.GroupBy(info => info.Volumes);
        foreach (var volume in volumes)
        {
            var infos = fileInfos.Where(info => info.Volumes == volume.Key).ToList();
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
                    .OrderByNatural(info => Parser.RemoveExtensionIfSupported(info.Filename)!)
                    .ToList();

                foreach (var chapter in chapters)
                {
                    chapter.IssueOrder = counter;
                    counter++;
                }
                continue;
            }

            chapters = infos
                .OrderBy(GetSortKey)
                .ThenBy(info => info.Chapters, StringComparer.OrdinalIgnoreCase)
                .ToList();

            counter = 0f;
            float? prevBase = null;

            foreach (var chapter in chapters)
            {
                var chapterNum = Parser.IsRange(chapter.Chapters) ?
                    $"{Parser.MinNumberFromRange(chapter.Chapters).ToString(CultureInfo.InvariantCulture)}"
                    : chapter.Chapters;

                if (float.TryParse(chapterNum, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedChapter))
                {
                    counter = parsedChapter;
                    if (prevBase.HasValue && parsedChapter.Is(prevBase.Value))
                    {
                        counter += 0.1f;
                    }
                    chapter.IssueOrder = counter;
                    prevBase = parsedChapter;
                }
                else
                {
                    // Pull out the leading numeric part, e.g. "15.UH" -> 15, "15.BEY" -> 15
                    var currentBase = ParseLeadingFloat(chapter.Chapters);

                    if (currentBase.HasValue && prevBase.HasValue && currentBase.Value.Is(prevBase.Value))
                    {
                        // Same base number as the previous entry -> keep bumping within the group
                        counter += 0.1f;
                    }
                    else if (currentBase.HasValue)
                    {
                        counter = currentBase.Value;
                    }
                    else
                    {
                        counter++;
                    }

                    chapter.IssueOrder = counter;
                    prevBase = currentBase ?? prevBase;
                }
            }
        }

        return;

        // Ensure chapters are sorted numerically when possible. For entries that don't parse
        // cleanly as a float, fall back to their leading numeric prefix (e.g. "15.HU" -> 15)
        // so they stay adjacent to their numeric siblings instead of being pushed to the end.
        // Only entries with no numeric component at all fall back to float.MaxValue.
        float GetSortKey(ParserInfo info)
        {
            var chapterNum = Parser.IsRange(info.Chapters)
                ? Parser.MinNumberFromRange(info.Chapters).ToString(CultureInfo.InvariantCulture)
                : info.Chapters;

            if (float.TryParse(chapterNum, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return val;
            }

            // Items parsed from leader should always be behind items without ('1' before '1 (A Story)').
            // Offset them slightly
            var leading = ParseLeadingFloat(info.Chapters);
            return leading.HasValue ? leading.Value + 0.0001f : float.MaxValue;
        }

        float? ParseLeadingFloat(string input)
        {
            var leadingMatch = LeadingFloatRegex().Match(input);
            if (!leadingMatch.Success) return null;

            if (float.TryParse(leadingMatch.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var lb))
            {
                return lb;
            }

            return null;
        }
    }

    [GeneratedRegex(@"^\d+(\.\d+)?")]
    private static partial Regex LeadingFloatRegex();
}
