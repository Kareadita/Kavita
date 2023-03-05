using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Extensions;
using API.Parser;
using API.SignalR;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;

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

public class SeriesModified
{
    public required string FolderPath { get; set; }
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
    /// <param name="folderAction">A callback async Task to be called once all files for each folder path are found</param>
    /// <param name="forceCheck">If we should bypass any folder last write time checks on the scan and force I/O</param>
    public async Task ProcessFiles(string folderPath, bool scanDirectoryByDirectory,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Func<IList<string>, string,Task> folderAction, bool forceCheck = false)
    {
        string normalizedPath;
        if (scanDirectoryByDirectory)
        {
            // This is used in library scan, so we should check first for a ignore file and use that here as well
            var potentialIgnoreFile = _directoryService.FileSystem.Path.Join(folderPath, DirectoryService.KavitaIgnoreFile);
            var matcher = _directoryService.CreateMatcherFromFile(potentialIgnoreFile);
            var directories = _directoryService.GetDirectories(folderPath, matcher).ToList();

            foreach (var directory in directories)
            {
                normalizedPath = Parser.Parser.NormalizePath(directory);
                if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedPath, forceCheck))
                {
                    await folderAction(new List<string>(), directory);
                }
                else
                {
                    // For a scan, this is doing everything in the directory loop before the folder Action is called...which leads to no progress indication
                    await folderAction(_directoryService.ScanFiles(directory, matcher), directory);
                }
            }

            return;
        }

        normalizedPath = Parser.Parser.NormalizePath(folderPath);
        if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedPath, forceCheck))
        {
            await folderAction(new List<string>(), folderPath);
            return;
        }
        // We need to calculate all folders till library root and see if any kavitaignores
        var seriesMatcher = BuildIgnoreFromLibraryRoot(folderPath, seriesPaths);

        await folderAction(_directoryService.ScanFiles(folderPath, seriesMatcher), folderPath);
    }

    /// <summary>
    /// Used in ScanSeries, which enters at a lower level folder and hence needs a .kavitaignore from higher (up to root) to be built before
    /// the scan takes place.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="seriesPaths"></param>
    /// <returns>A GlobMatter. Empty if not applicable</returns>
    private GlobMatcher BuildIgnoreFromLibraryRoot(string folderPath, IDictionary<string, IList<SeriesModified>> seriesPaths)
    {
        var seriesMatcher = new GlobMatcher();
        try
        {
            var roots = seriesPaths[folderPath][0].LibraryRoots.Select(Parser.Parser.NormalizePath).ToList();
            var libraryFolder = roots.SingleOrDefault(folderPath.Contains);

            if (string.IsNullOrEmpty(libraryFolder) || !Directory.Exists(folderPath))
            {
                return seriesMatcher;
            }

            var allParents = _directoryService.GetFoldersTillRoot(libraryFolder, folderPath);
            var path = libraryFolder;

            // Apply the library root level kavitaignore
            var potentialIgnoreFile = _directoryService.FileSystem.Path.Join(path, DirectoryService.KavitaIgnoreFile);
            seriesMatcher.Merge(_directoryService.CreateMatcherFromFile(potentialIgnoreFile));

            // Then apply kavitaignores for each folder down to where the series folder is
            foreach (var folderPart in allParents.Reverse())
            {
                path = Parser.Parser.NormalizePath(Path.Join(libraryFolder, folderPart));
                potentialIgnoreFile = _directoryService.FileSystem.Path.Join(path, DirectoryService.KavitaIgnoreFile);
                seriesMatcher.Merge(_directoryService.CreateMatcherFromFile(potentialIgnoreFile));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ScannerService] There was an error trying to find and apply .kavitaignores above the Series Folder. Scanning without them present");
        }

        return seriesMatcher;
    }


    /// <summary>
    /// Attempts to either add a new instance of a show mapping to the _scannedSeries bag or adds to an existing.
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

            scannedSeries.AddOrUpdate(existingKey, new List<ParserInfo>() {info}, (_, oldValue) =>
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
    /// <param name="libraryType"></param>
    /// <param name="folders"></param>
    /// <param name="libraryName"></param>
    /// <param name="isLibraryScan">If true, does a directory scan first (resulting in folders being tackled in parallel), else does an immediate scan files</param>
    /// <param name="seriesPaths">A map of Series names -> existing folder paths to handle skipping folders</param>
    /// <param name="processSeriesInfos">Action which returns if the folder was skipped and the infos from said folder</param>
    /// <param name="forceCheck">Defaults to false</param>
    /// <returns></returns>
    public async Task ScanLibrariesForSeries(LibraryType libraryType,
        IEnumerable<string> folders, string libraryName, bool isLibraryScan,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Func<Tuple<bool, IList<ParserInfo>>, Task>? processSeriesInfos, bool forceCheck = false)
    {

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Starting", libraryName, ProgressEventType.Started));

        async Task ProcessFolder(IList<string> files, string folder)
        {
            var normalizedFolder = Parser.Parser.NormalizePath(folder);
            if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedFolder, forceCheck))
            {
                var parsedInfos = seriesPaths[normalizedFolder].Select(fp => new ParserInfo()
                {
                    Series = fp.SeriesName,
                    Format = fp.Format,
                }).ToList();
                if (processSeriesInfos != null)
                    await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(true, parsedInfos));
                _logger.LogDebug("[ScannerService] Skipped File Scan for {Folder} as it hasn't changed since last scan", folder);
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.FileScanProgressEvent("Skipped " + normalizedFolder, libraryName, ProgressEventType.Updated));
                return;
            }

            _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, folder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent(folder, libraryName, ProgressEventType.Updated));
            if (files.Count == 0)
            {
                _logger.LogInformation("[ScannerService] {Folder} is empty or is no longer in this location", folder);
                return;
            }

            var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
            var infos = files
                .Select(file => _readingItemService.ParseFile(file, folder, libraryType))
                .Where(info => info != null)
                .ToList();


            MergeLocalizedSeriesWithSeries(infos!);

            foreach (var info in infos)
            {
                try
                {
                    TrackSeries(scannedSeries, info);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[ScannerService] There was an exception that occurred during tracking {FilePath}. Skipping this file",
                        info?.FullFilePath);
                }
            }

            foreach (var series in scannedSeries.Keys)
            {
                if (scannedSeries[series].Count > 0 && processSeriesInfos != null)
                {
                    await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(false, scannedSeries[series]));
                }
            }
        }


        foreach (var folderPath in folders)
        {
            try
            {
                await ProcessFiles(folderPath, isLibraryScan, seriesPaths, ProcessFolder, forceCheck);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folderPath);
            }
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Done", libraryName, ProgressEventType.Ended));
    }

    /// <summary>
    /// Checks against all folder paths on file if the last scanned is >= the directory's last write down to the second
    /// </summary>
    /// <param name="seriesPaths"></param>
    /// <param name="normalizedFolder"></param>
    /// <param name="forceCheck"></param>
    /// <returns></returns>
    private bool HasSeriesFolderNotChangedSinceLastScan(IDictionary<string, IList<SeriesModified>> seriesPaths, string normalizedFolder, bool forceCheck = false)
    {
        if (forceCheck) return false;

        return seriesPaths.ContainsKey(normalizedFolder) && seriesPaths[normalizedFolder].All(f => f.LastScanned.Truncate(TimeSpan.TicksPerSecond) >=
            _directoryService.GetLastWriteTime(normalizedFolder).Truncate(TimeSpan.TicksPerSecond));
    }

    /// <summary>
    /// Checks if there are any ParserInfos that have a Series that matches the LocalizedSeries field in any other info. If so,
    /// rewrites the infos with series name instead of the localized name, so they stack.
    /// </summary>
    /// <example>
    /// Accel World v01.cbz has Series "Accel World" and Localized Series "World of Acceleration"
    /// World of Acceleration v02.cbz has Series "World of Acceleration"
    /// After running this code, we'd have:
    /// World of Acceleration v02.cbz having Series "Accel World" and Localized Series of "World of Acceleration"
    /// </example>
    /// <param name="infos">A collection of ParserInfos</param>
    private void MergeLocalizedSeriesWithSeries(IReadOnlyCollection<ParserInfo> infos)
    {
        var hasLocalizedSeries = infos.Any(i => !string.IsNullOrEmpty(i.LocalizedSeries));
        if (!hasLocalizedSeries) return;

        var localizedSeries = infos
            .Where(i => !i.IsSpecial)
            .Select(i => i.LocalizedSeries)
            .Distinct()
            .FirstOrDefault(i => !string.IsNullOrEmpty(i));
        if (string.IsNullOrEmpty(localizedSeries)) return;

        // NOTE: If we have multiple series in a folder with a localized title, then this will fail. It will group into one series. User needs to fix this themselves.
        string? nonLocalizedSeries;
        // Normalize this as many of the cases is a capitalization difference
        var nonLocalizedSeriesFound = infos
            .Where(i => !i.IsSpecial)
            .Select(i => i.Series).DistinctBy(Parser.Parser.Normalize).ToList();
        if (nonLocalizedSeriesFound.Count == 1)
        {
            nonLocalizedSeries = nonLocalizedSeriesFound.First();
        }
        else
        {
            // There can be a case where there are multiple series in a folder that causes merging.
            if (nonLocalizedSeriesFound.Count > 2)
            {
                _logger.LogError("[ScannerService] There are multiple series within one folder that contain localized series. This will cause them to group incorrectly. Please separate series into their own dedicated folder or ensure there is only 2 potential series (localized and series):  {LocalizedSeries}", string.Join(", ", nonLocalizedSeriesFound));
            }
            nonLocalizedSeries = nonLocalizedSeriesFound.FirstOrDefault(s => !s.Equals(localizedSeries));
        }

        if (nonLocalizedSeries == null) return;

        var normalizedNonLocalizedSeries = nonLocalizedSeries.ToNormalized();
        foreach (var infoNeedingMapping in infos.Where(i =>
                     !i.Series.ToNormalized().Equals(normalizedNonLocalizedSeries)))
        {
            infoNeedingMapping.Series = nonLocalizedSeries;
            infoNeedingMapping.LocalizedSeries = localizedSeries;
        }
    }
}
