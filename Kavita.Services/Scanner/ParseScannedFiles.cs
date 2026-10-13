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
    private static readonly TimeSpan ListingEventInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IEventHub _eventHub;

    /// <summary>
    /// File id to the write time from the listing, for unchanged files that have none stored yet
    /// </summary>
    /// <remarks>v0.9.2 added this, after one full stable, this can be removed as one scan will backfill times</remarks>
    public Dictionary<int, DateTime> WriteTimesToBackfill { get; } = new();

    private readonly Dictionary<string, ScanIssue> _issues = new();
    private readonly Dictionary<string, IList<string>> _filesInIssueFolders = new();
    private readonly HashSet<string> _failedFilesInReadFolders = [];
    private readonly Dictionary<string, FileStamp> _readFiles = new();
    private readonly HashSet<string> _readFolders = [];
    private readonly HashSet<string> _walkedFolders = [];
    private readonly HashSet<string> _keptFiles = [];
    private readonly List<string> _unreadableFolders = [];

    /// <summary>
    /// Issues with files that were read this scan, at most one per path
    /// </summary>
    public IReadOnlyCollection<ScanIssue> Issues => _issues.Values;

    /// <summary>
    /// For each folder with an issue, the normalized paths of every file listed directly in it
    /// </summary>
    public IReadOnlyDictionary<string, IList<string>> FilesInIssueFolders => _filesInIssueFolders;

    /// <summary>
    /// Earlier failures in folders that were read this scan, whose file is gone or has no issue anymore
    /// </summary>
    public IEnumerable<string> ResolvedFailedFiles => _failedFilesInReadFolders.Where(path => !_issues.ContainsKey(path));

    /// <summary>
    /// Every file listed in a folder that was read this scan, by normalized path
    /// </summary>
    public IReadOnlyDictionary<string, FileStamp> ReadFiles => _readFiles;

    /// <summary>
    /// Folders whose listing was read this scan. A file in one of these and not in <see cref="ReadFiles"/> is gone
    /// </summary>
    public IReadOnlySet<string> ReadFolders => _readFolders;

    /// <summary>
    /// Folders that could not be read this scan, their series were kept as they are
    /// </summary>
    public IReadOnlyList<string> UnreadableFolders => _unreadableFolders;

    /// <summary>
    /// Time spent listing the folder tree of a library scan, summed over every folder passed to <see cref="ScanLibrariesForSeries"/>
    /// </summary>
    public TimeSpan ListTime { get; private set; }

    /// <summary>
    /// Time spent walking the listed folders and checking them for changes, summed like <see cref="ListTime"/>.
    /// A series scan lists its folder here
    /// </summary>
    public TimeSpan WalkTime { get; private set; }

    /// <summary>
    /// Time spent parsing the files of changed folders, summed like <see cref="WalkTime"/>
    /// </summary>
    public TimeSpan ParseTime { get; private set; }

    /// <summary>
    /// Time spent merging localized names and grouping parsed files into series, summed like <see cref="WalkTime"/>
    /// </summary>
    public TimeSpan GroupTime { get; private set; }

    /// <summary>
    /// Files opened and parsed this scan
    /// </summary>
    public int ParsedFileCount => _parsedFileCount;
    private int _parsedFileCount;

    /// <summary>
    /// Files in read folders that were not parsed because they match the database
    /// </summary>
    public int KeptFileCount { get; private set; }

    /// <summary>
    /// The folder was not read, walked, or inside a folder that could not be read this scan, so it may be gone
    /// </summary>
    public bool IsUnaccountedFor(string folder)
    {
        return !_readFolders.Contains(folder) && !_walkedFolders.Contains(folder)
                                              && !_unreadableFolders.Exists(folder.IsSameOrInsideFolder);
    }

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
    /// <param name="known">The series in each folder, and whether the scan is forced</param>
    /// <param name="folderPath">A library folder or series folder</param>
    /// <param name="failedFiles">Files that failed on an earlier scan, known while unchanged</param>
    public async Task<IList<ScanResult>> ScanFiles(string folderPath, bool scanDirectoryByDirectory,
        KnownSeriesFolders known, Library library, IReadOnlyList<FailedFile>? failedFiles = null)
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
            return await ScanDirectories(folderPath, known, failures, library, matcher, result, fileExtensions);
        }

        return await ScanSingleDirectory(folderPath, known, failures, library, result, fileExtensions, matcher);
    }

    private async Task<IList<ScanResult>> ScanDirectories(string folderPath, KnownSeriesFolders known,
        IReadOnlyList<FailedFile> failedFiles, Library library, GlobMatcher matcher, List<ScanResult> result, string fileExtensions)
    {
        var listWatch = Stopwatch.StartNew();
        var nextListingEvent = TimeSpan.Zero;
        var tree = await _directoryService.ListFolderTreeAsync(folderPath, fileExtensions, matcher, folder =>
        {
            if (listWatch.Elapsed < nextListingEvent) return Task.CompletedTask;

            nextListingEvent = listWatch.Elapsed + ListingEventInterval;
            return _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(folder, library.Id, library.Name, ProgressEventType.Updated,
                    MessageEventCode.ScanListingFolders));
        });
        ListTime += listWatch.Elapsed;
        var allDirectories = tree.Folders;

        var processedDirsAndAncestors = new HashSet<string>();
        var resultFolders = new HashSet<string>();
        var skippedSpecials = new List<string>();
        var total = allDirectories.Count;
        var timings = new DirectoryScanTimings();
        var loopSw = Stopwatch.StartNew();

        _logger.LogDebug("[ScannerService] Step 1.C Found {DirectoryCount} directories to process for {FolderPath}", total, folderPath);
        for (var i = 0; i < total; i++)
        {
            var directory = allDirectories[i];
            _walkedFolders.Add(directory);

            timings.Events.Start();
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(directory, library.Id, library.Name, ProgressEventType.Updated,
                    MessageEventCode.ScanListingFolders, i + 1, total));
            timings.Events.Stop();

            var isParent = processedDirsAndAncestors.Contains(directory);
            try
            {
                // Subfolders were already read, so only the loose files at this level and its Specials folders are left
                if (isParent)
                {
                    timings.ParentCount++;
                    timings.ParentChangeCheck.Start();

                    var specials = skippedSpecials.Where(s => IsDirectChild(s, directory)).ToList();
                    var looseFileOwners = known.OwnersToCompareIn(directory);
                    var specialsOwners = specials.Select(known.OwnersToCompareIn).ToList();
                    var allOwners = looseFileOwners.Concat(specialsOwners.SelectMany(o => o)).Distinct().ToList();

                    var onDisk = LibraryFiles(tree.FilesIn(directory).Concat(specials.SelectMany(tree.FilesBelow)), library.Type);

                    Func<string, bool> isInScope = folder => folder == directory || specials.Exists(folder.IsSameOrInsideFolder);
                    var failures = FailedFilesIn(failedFiles, isInScope);
                    var unchanged = known.IsUnchanged(onDisk, allOwners, failures, isInScope, WriteTimesToBackfill);

                    timings.ParentChangeCheck.Stop();
                    timings.ParentSurfaceFiles.Start();

                    if (unchanged)
                    {
                        if (looseFileOwners.Count > 0)
                        {
                            HandleUnchangedFolder(result, resultFolders, folderPath, directory, looseFileOwners, true);
                        }

                        for (var j = 0; j < specials.Count; j++)
                        {
                            if (specialsOwners[j].Count == 0) continue;
                            HandleUnchangedFolder(result, resultFolders, folderPath, specials[j], specialsOwners[j], false);
                        }
                    }
                    else
                    {
                        MarkRead(failures);
                        // With no files left there is no result, and AddReadFiles would never mark these folders read
                        _readFolders.Add(directory);
                        _readFolders.UnionWith(specials);
                        AddSurfaceAndSpecialsFiles(result, resultFolders, directory, folderPath, onDisk);
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
                var owners = skippedSpecials
                    .Where(s => s.IsInsideFolder(directory))
                    .Prepend(directory)
                    .SelectMany(known.OwnersToCompareIn)
                    .Distinct()
                    .ToList();

                var onDiskBelow = LibraryFiles(tree.FilesBelow(directory), library.Type);
                Func<string, bool> isBelow = folder => folder.IsSameOrInsideFolder(directory);
                var failuresBelow = FailedFilesIn(failedFiles, isBelow);

                if (known.IsUnchanged(onDiskBelow, owners, failuresBelow, isBelow, WriteTimesToBackfill))
                {
                    HandleUnchangedFolder(result, resultFolders, folderPath, directory, owners, false);
                }
                else
                {
                    MarkRead(failuresBelow);
                    AddChangedFolder(result, resultFolders, directory, folderPath, onDiskBelow);
                }

                AddWithAncestors(processedDirsAndAncestors, directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                timings.ParentChangeCheck.Stop();
                timings.ParentSurfaceFiles.Stop();
                _logger.LogWarning(ex, "[ScannerService] Could not read {Directory}, keeping its series as they are for this scan", directory);
                _unreadableFolders.Add(directory);
                KeepUnreadableFolder(result, resultFolders, folderPath, directory, isParent, skippedSpecials, known);
                if (!isParent)
                {
                    AddWithAncestors(processedDirsAndAncestors, directory);
                }
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
    private void KeepUnreadableFolder(List<ScanResult> result, HashSet<string> resultFolders, string folderPath, string directory, bool isParent,
        IEnumerable<string> skippedSpecials, KnownSeriesFolders known)
    {
        if (!isParent)
        {
            var owners = known.OwnersInside(directory);
            if (owners.Count > 0)
            {
                HandleUnchangedFolder(result, resultFolders, folderPath, directory, owners, false);
            }

            return;
        }

        var looseFileOwners = known.OwnersIn(directory);
        if (looseFileOwners.Count > 0) HandleUnchangedFolder(result, resultFolders, folderPath, directory, looseFileOwners, true);

        foreach (var special in skippedSpecials.Where(s => IsDirectChild(s, directory)))
        {
            var specialOwners = known.OwnersIn(special);
            if (specialOwners.Count > 0)
            {
                HandleUnchangedFolder(result, resultFolders, folderPath, special, specialOwners, false);
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

    /// <summary>
    /// Files the parsers would turn into series files. Anything else would make the folder look changed on every scan
    /// </summary>
    private static List<FileStamp> LibraryFiles(IEnumerable<FileStamp> files, LibraryType type)
    {
        return files.Where(f => !Parser.IsSkippedCoverImage(Path.GetFileName(f.Path), type)).ToList();
    }

    private List<FileStamp> ListLibraryFiles(string directory, string fileExtensions, GlobMatcher matcher, LibraryType type)
    {
        return LibraryFiles(_directoryService.ScanFiles(directory, fileExtensions, matcher), type);
    }

    /// <summary>
    /// Handles directories that haven't changed since the last scan.
    /// </summary>
    private void HandleUnchangedFolder(List<ScanResult> result, HashSet<string> resultFolders, string folderPath, string directory,
        IList<SeriesModified> owners, bool isShallow)
    {
        if (resultFolders.Contains(directory))
        {
            _logger.LogDebug("[ProcessFiles] Skipping adding {Directory} as it's already added, this indicates a bad layout issue", directory);
        }
        else
        {
            _logger.LogDebug("[ProcessFiles] Skipping {Directory} as it hasn't changed since last scan", directory);
            var unchanged = CreateScanResult(directory, folderPath, false, ArraySegment<FileStamp>.Empty);
            unchanged.UnchangedSeries = owners;
            unchanged.IsShallow = isShallow;
            AddResult(result, resultFolders, unchanged);
        }
    }

    private void AddChangedFolder(List<ScanResult> result, HashSet<string> resultFolders, string directory, string folderPath, List<FileStamp> files)
    {
        _logger.LogDebug("[ProcessFiles] Performing full scan on {Directory}", directory);
        if (files.Count == 0)
        {
            _logger.LogDebug("[ProcessFiles] Empty directory: {Directory}. Keeping empty will cause Kavita to scan this each time", directory);
        }
        AddResult(result, resultFolders, CreateScanResult(directory, folderPath, true, files));
    }

    /// <summary>
    /// Reads the files directly in the directory and everything in the given Specials folders.
    /// They share one result so the files are parsed with the directory as root, else the series would be named "Specials"
    /// </summary>
    private static void AddSurfaceAndSpecialsFiles(List<ScanResult> result, HashSet<string> resultFolders, string directory, string folderPath,
        List<FileStamp> files)
    {
        if (files.Count == 0)
        {
            return;
        }
        AddResult(result, resultFolders, CreateScanResult(directory, folderPath, true, files));
    }

    private static void AddResult(List<ScanResult> result, HashSet<string> resultFolders, ScanResult scanResult)
    {
        resultFolders.Add(scanResult.Folder);
        result.Add(scanResult);
    }

    /// <summary>
    /// Adds the folder and every folder above it, stopping at the first one already in the set
    /// </summary>
    private static void AddWithAncestors(HashSet<string> folders, string folder)
    {
        var current = folder;
        while (current.Length > 0 && folders.Add(current))
        {
            current = ParentOf(current);
        }
    }

    private static string ParentOf(string folder)
    {
        var lastSlash = folder.LastIndexOf('/');
        return lastSlash <= 0 ? string.Empty : folder[..lastSlash];
    }

    private static bool IsDirectChild(string folder, string parent)
    {
        var lastSlash = folder.LastIndexOf('/');
        return lastSlash > 0 && folder.AsSpan(0, lastSlash).Equals(parent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Scans a single directory and processes the scan result.
    /// </summary>
    private async Task<IList<ScanResult>> ScanSingleDirectory(string folderPath, KnownSeriesFolders known,
        IReadOnlyList<FailedFile> failedFiles, Library library, List<ScanResult> result,
        string fileExtensions, GlobMatcher matcher)
    {
        var normalizedPath = Parser.NormalizePath(folderPath);
        var libraryRoot =
            library.Folders.FirstOrDefault(f => normalizedPath.IsSameOrInsideFolder(f.Path))?.Path ??
            folderPath;

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent(normalizedPath, library.Id, library.Name, ProgressEventType.Updated,
                MessageEventCode.ScanListingFolders, 1, 1));

        List<FileStamp> onDisk;
        try
        {
            onDisk = ListLibraryFiles(folderPath, fileExtensions, matcher, library.Type);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "[ScannerService] Could not read {Directory}, keeping its series as they are for this scan", normalizedPath);
            _unreadableFolders.Add(normalizedPath);
            KeepUnreadableFolder(result, [], libraryRoot, normalizedPath, false, [], known);
            return result;
        }

        // Every series with files inside, so a folder shared by several series does not see the others' files as new
        var canCompare = known.CanCompare(normalizedPath);
        var owners = canCompare ? known.OwnersInside(normalizedPath) : [];

        Func<string, bool> isInside = folder => folder.IsSameOrInsideFolder(normalizedPath);
        var failures = FailedFilesIn(failedFiles, isInside);
        var knownFailures = canCompare ? failures : [];

        if (known.IsUnchanged(onDisk, owners, knownFailures, isInside, WriteTimesToBackfill))
        {
            var unchanged = CreateScanResult(folderPath, libraryRoot, false, ArraySegment<FileStamp>.Empty);
            unchanged.UnchangedSeries = known.SeriesAt(normalizedPath);
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
        var index = new SeriesNameIndex(scannedSeries);

        // Iterate through each ParserInfo and track the series
        foreach (var info in allInfos)
        {
            if (info == null) continue;

            try
            {
                var rejection = TrackSeries(scannedSeries, index, info);
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
    /// Adds the info to the series whose normalized name matches its series, sort or localized name, else to a new series.
    /// A match keeps the series' spelling, as punctuation or capitalization may differ between files
    /// </summary>
    /// <param name="scannedSeries">A localized list of a series' parsed infos</param>
    /// <param name="index">Name lookup over the keys of <paramref name="scannedSeries"/></param>
    /// <param name="info"></param>
    /// <returns>Set when the file is turned away</returns>
    private ParseIssue? TrackSeries(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, SeriesNameIndex index,
        ParserInfo? info)
    {
        if (info == null) return null;
        if (info.Series == string.Empty) return ParseIssues.FromFailedParse(info);

        var normalizedSeries = info.Series.ToNormalized();

        // Do not ingest series with no meaningful information as title. These break merging as they'll all merge into each other
        // They would also merge all series that don't have localized names previously
        // This does create the edge case where some series may really not have any meaningful information.
        // But until this is reported, we should ignore it and play it safe!
        if (string.IsNullOrEmpty(normalizedSeries))
        {
            _logger.LogCritical("[ScannerService] {SeriesName} @ {FileName} is empty when normalized, this file will not be ingested! The filename does not follow our guidelines or this is a bug in the parser, please report this! https://github.com/Kareadita/Kavita/issues",
                info.Series, info.Filename);

            return new ParseIssue(MediaErrorReason.NoSeriesName,
                $"{info.Series} is empty when normalized, this file will not be ingested! The filename does not follow our guidelines or this is a bug in the parser, please report this! https://github.com/Kareadita/Kavita/issues");
        }

        // BUG: This will fail for Solo Leveling & Solo Leveling (Manga)
        var matches = index.Find(info.Format, normalizedSeries, info.SeriesSort.ToNormalized(), info.LocalizedSeries.ToNormalized());
        if (matches.Count > 1)
        {
            var names = matches.Select(m => m.Name).ToList();
            _logger.LogWarning("[ScannerService] {FilePath} matches {MatchCount} series ({SeriesNames}), it was not added to any of them",
                info.FullFilePath, names.Count, string.Join(", ", names));
            return ParseIssues.FromAmbiguousSeries(info, names);
        }

        var key = matches.Count == 1
            ? matches[0]
            : index.Add(new ParsedSeries
            {
                Format = info.Format,
                Name = info.Series,
                NormalizedName = normalizedSeries
            });

        info.Series = key.Name;
        if (!index.Track(info)) return null;

        scannedSeries.AddOrUpdate(key, [info], (_, infos) =>
        {
            infos.Add(info);
            return infos;
        });

        return null;
    }

    /// <summary>
    /// The series found so far by format and normalized name, so a file finds its series in one lookup per name
    /// </summary>
    private sealed class SeriesNameIndex
    {
        private readonly Dictionary<(MangaFormat Format, string NormalizedName), ParsedSeries> _keys = new();
        private readonly HashSet<ParserInfo> _tracked = new(ReferenceEqualityComparer.Instance);

        public SeriesNameIndex(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries)
        {
            foreach (var (key, infos) in scannedSeries)
            {
                Add(key);
                _tracked.UnionWith(infos);
            }
        }

        public ParsedSeries Add(ParsedSeries key)
        {
            if (!string.IsNullOrEmpty(key.NormalizedName))
            {
                _keys.TryAdd((key.Format, key.NormalizedName), key);
            }

            return key;
        }

        /// <returns>False when the info was already tracked</returns>
        public bool Track(ParserInfo info)
        {
            return _tracked.Add(info);
        }

        /// <returns>Each distinct series of this format named by any of the normalized names</returns>
        public List<ParsedSeries> Find(MangaFormat format, string normalizedName, string normalizedSortName,
            string normalizedLocalizedName)
        {
            var matches = new List<ParsedSeries>(1);
            AddMatch(matches, format, normalizedName);
            AddMatch(matches, format, normalizedSortName);
            AddMatch(matches, format, normalizedLocalizedName);
            return matches;
        }

        private void AddMatch(List<ParsedSeries> matches, MangaFormat format, string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName)) return;
            if (!_keys.TryGetValue((format, normalizedName), out var key) || matches.Contains(key)) return;

            matches.Add(key);
        }
    }

    /// <summary>
    /// This will process series by folder groups. This is used by ScanSeries and ScanLibrary
    /// </summary>
    /// <param name="library">This should have the FileTypes included</param>
    /// <param name="folders">Library roots for a library scan, series folders for a series scan</param>
    /// <param name="isLibraryScan">If true, walks each folder's tree folder by folder, else lists each folder as one</param>
    /// <param name="known">The series in each folder, and whether the scan is forced</param>
    /// <param name="failedFiles">Files that failed on an earlier scan, known while unchanged</param>
    public async Task<IList<ScannedSeriesResult>> ScanLibrariesForSeries(Library library,
        IList<string> folders, bool isLibraryScan, KnownSeriesFolders known,
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
                var scanResults = await ScanAndParseFolder(folder, library, isLibraryScan, known,
                    processedScannedSeries, placeholderOwners, failedFiles);
                unchangedFolders.AddRange(scanResults.Where(r => !r.HasChanged));
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folder);
            }
        }

        FlagSeriesWithMissingFiles(processedScannedSeries, folders, known, unchangedFolders, placeholderOwners);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent("File Scan Done", library.Id, library.Name, ProgressEventType.Ended));

        return processedScannedSeries.ToList();
    }

    /// <param name="processedScannedSeries">Gets the series found in the folder</param>
    /// <param name="placeholderOwners">Filled with the series each placeholder was made for</param>
    /// <returns>Every folder result of the walk</returns>
    private async Task<IList<ScanResult>> ScanAndParseFolder(string folderPath, Library library,
        bool isLibraryScan, KnownSeriesFolders known,
        ConcurrentBag<ScannedSeriesResult> processedScannedSeries,
        Dictionary<ParserInfo, SeriesModified> placeholderOwners, IReadOnlyList<FailedFile>? failedFiles)
    {
        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.B: Scan files in {Folder}", library.Name, folderPath);
        var stepWatch = Stopwatch.StartNew();
        var listedBefore = ListTime;
        var scanResults = await ScanFiles(folderPath, isLibraryScan, known, library, failedFiles);
        WalkTime += stepWatch.Elapsed - (ListTime - listedBefore);
        var walkResults = scanResults;

        AddReadFiles(walkResults);

        // Aggregate the scanned series across all scanResults
        var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.C: Process files in {Folder}", library.Name, folderPath);
        stepWatch.Restart();
        for (var i = 0; i < scanResults.Count; i++)
        {
            await ParseFiles(scanResults[i], known, placeholderOwners, library, i + 1, scanResults.Count);
        }
        ParseTime += stepWatch.Elapsed;

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.FileScanProgressEvent(folderPath, library.Id, library.Name, ProgressEventType.Updated,
                MessageEventCode.ScanGroupingSeries));

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.D: Merge any localized series with series {Folder}", library.Name, folderPath);
        stepWatch.Restart();
        scanResults = MergeLocalizedSeriesAcrossScanResults(scanResults);

        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.E: Group all parsed data into logical Series", library.Name);
        var rejected = TrackSeriesAcrossScanResults(scanResults, scannedSeries);
        AddRejectedIssues(walkResults, rejected);


        // Now transform and add to processedScannedSeries AFTER everything is processed
        _logger.LogDebug("\t[ScannerService] Library {LibraryName} Step 1.F: Generate Sort Order for Series and Finalize", library.Name);
        GenerateProcessedScannedSeries(scannedSeries, processedScannedSeries);
        GroupTime += stepWatch.Elapsed;

        return walkResults;
    }

    private void AddReadFiles(IList<ScanResult> walkResults)
    {
        foreach (var result in walkResults.Where(r => r.HasChanged))
        {
            _readFolders.Add(Parser.NormalizePath(result.Folder));
            foreach (var stamp in result.Files)
            {
                var path = Parser.NormalizePath(stamp.Path);
                _readFiles.TryAdd(path, stamp);
                _readFolders.Add(path.FolderOf());
            }
        }
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
            AddIssue(stamp, issue, stamps.Values);
        }
    }

    /// <param name="listing">Files listed with the one that has the issue</param>
    private void AddIssue(FileStamp stamp, ParseIssue issue, IEnumerable<FileStamp> listing)
    {
        var scanIssue = ScanIssue.From(stamp, issue);
        _issues[scanIssue.Path] = scanIssue;

        var folder = scanIssue.Path.FolderOf();
        if (_filesInIssueFolders.ContainsKey(folder)) return;
        _filesInIssueFolders[folder] = listing.Select(f => Parser.NormalizePath(f.Path)).Where(p => p.FolderOf() == folder).ToList();
    }

    /// <summary>
    /// A series may have lost files when one of its known files is in a folder that was not skipped as unchanged and was not
    /// kept by <see cref="ParseFiles"/> (file deleted or moved to another series, folder deleted or emptied). It has only
    /// placeholders, so it is marked to be processed anyway
    /// </summary>
    private void FlagSeriesWithMissingFiles(IEnumerable<ScannedSeriesResult> processedScannedSeries,
        IList<string> scannedFolders, KnownSeriesFolders known,
        IList<ScanResult> unchangedFolders, Dictionary<ParserInfo, SeriesModified> placeholderOwners)
    {
        if (placeholderOwners.Count == 0) return;

        var roots = scannedFolders.Select(Parser.NormalizePath).ToList();
        var shallow = unchangedFolders.Where(r => r.IsShallow).Select(r => r.Folder.TrimEnd('/')).ToHashSet();
        var recursive = unchangedFolders.Where(r => !r.IsShallow).Select(r => r.Folder.TrimEnd('/')).ToHashSet();

        var seriesWithMissingFiles = known.AllSeries
            .Where(s => s.FilesByFolder
                .Any(f => roots.Exists(f.Key.IsSameOrInsideFolder) && !IsCovered(f.Key) && !IsKept(f.Value)))
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

        // A file that was parsed again may now belong to another series, so only kept files count
        bool IsKept(IReadOnlyList<KnownFile> files)
        {
            return files.All(f => _keptFiles.Contains(f.Path));
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
    /// For a given ScanResult, sets the ParserInfos on the result. In a changed folder, files that match the database are kept
    /// behind a placeholder per series instead of parsed, unless a file in the folder is sorted by its siblings
    /// (<see cref="KnownFile.IsSortedBySiblings"/>)
    /// </summary>
    /// <param name="known">Which files the database holds, nothing when forced</param>
    /// <param name="placeholderOwners">Filled with the series each placeholder was made for</param>
    private async Task ParseFiles(ScanResult result, KnownSeriesFolders known, Dictionary<ParserInfo, SeriesModified> placeholderOwners,
        Library library, int current, int total)
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

        var paths = files.Select(f => Parser.NormalizePath(f.Path)).ToList();
        var knownFiles = known.FilesIn(paths.Select(p => p.FolderOf()).Distinct());
        // A file with an issue row is parsed again, or the row would count as resolved and be deleted
        var kept = Enumerable.Range(0, fileCount)
            .Where(i => !_failedFilesInReadFolders.Contains(paths[i]) &&
                        knownFiles.TryGetValue(paths[i], out var owned) &&
                        FolderChangeCheck.IsFileUnchanged(files[i], owned.File, owned.Owner.LastScanned))
            .ToHashSet();
        if (kept.Any(i => knownFiles[paths[i]].File.SortOrderDependsOnSiblings))
        {
            kept.Clear();
        }

        // Written by index rather than collected, as downstream series mapping relies on file order
        var parsed = new ParseFileResult?[fileCount];
        await ParseAt(result, library, parsed, [.. Enumerable.Range(0, fileCount).Where(i => !kept.Contains(i))]);

        if (kept.Count > 0 && parsed.Any(p => p?.Info != null && KnownFile.IsSortedBySiblings(p.Info.Chapters, p.Info.IsSpecialInfo())))
        {
            await ParseAt(result, library, parsed, [.. kept]);
            kept.Clear();
        }

        var infos = new List<ParserInfo>(fileCount);
        for (var i = 0; i < fileCount; i++)
        {
            if (parsed[i] is not { } fileResult) continue;

            if (fileResult.Info != null)
            {
                fileResult.Info.Stamp = files[i];
                infos.Add(fileResult.Info);
            }
            if (fileResult.Issue != null)
            {
                AddIssue(files[i], fileResult.Issue, files);
            }
        }

        var keptByOwner = kept
            .Select(i => (Index: i, knownFiles[paths[i]].File, knownFiles[paths[i]].Owner))
            .GroupBy(k => k.Owner);
        foreach (var ownerFiles in keptByOwner)
        {
            foreach (var (index, file, _) in ownerFiles.Where(k => k.File.LastWriteTimeUtc == null))
            {
                WriteTimesToBackfill[file.Id] = files[index].LastWriteTimeUtc;
            }

            var placeholder = new ParserInfo
            {
                Series = ownerFiles.Key.SeriesName,
                Format = ownerFiles.Key.Format,
                UnchangedFiles = [.. ownerFiles.Select(k => k.File.Path)],
            };
            placeholderOwners[placeholder] = ownerFiles.Key;
            infos.Add(placeholder);
            _keptFiles.UnionWith(placeholder.UnchangedFiles);
        }

        KeptFileCount += kept.Count;
        if (kept.Count > 0)
        {
            _logger.LogDebug("[ScannerService] Kept {KeptCount} of {Count} files in {Folder} as they match the database", kept.Count, fileCount, normalizedFolder);
        }

        result.ParserInfos = infos;
    }

    private async Task ParseAt(ScanResult result, Library library, ParseFileResult?[] parsed, IList<int> indexes)
    {
        var files = result.Files;
        var folder = Parser.NormalizePath(result.Folder);
        Interlocked.Add(ref _parsedFileCount, indexes.Count);

        if (indexes.Count < 100)
        {
            foreach (var i in indexes)
            {
                parsed[i] = _readingItemService.ParseFile(files[i].Path, folder, result.LibraryRoot, library.Type, library.EnableMetadata);
            }
            return;
        }

        // Process files in parallel, but bound the concurrency so that a folder with
        // many files cannot flood the ThreadPool and starve request handling (the web UI
        // and health checks become unresponsive during scans otherwise).
        // Matches the scanner's existing parallelism convention (see ScannerService).
        var maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);

        await Parallel.ForEachAsync(indexes,
            new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency },
            (i, _) =>
            {
                parsed[i] = _readingItemService.ParseFile(files[i].Path, folder, result.LibraryRoot, library.Type, library.EnableMetadata);
                return ValueTask.CompletedTask;
            });
    }


    public static void UpdateSortOrder(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParsedSeries series)
    {
        // Placeholders from skipped folders don't map to a chapter, so they have nothing to sort
        var fileInfos = scannedSeries[series].Where(info => !info.IsPlaceholder).ToList();

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
