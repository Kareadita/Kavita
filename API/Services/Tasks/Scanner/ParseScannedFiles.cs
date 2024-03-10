using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
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
    public IList<ScanResult> ProcessFiles(string folderPath, bool scanDirectoryByDirectory,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Library library, bool forceCheck = false)
    {
        string normalizedPath;
        var result = new List<ScanResult>();
        var fileExtensions = string.Join("|", library.LibraryFileTypes.Select(l => l.FileTypeGroup.GetRegex()));
        if (scanDirectoryByDirectory)
        {
            // This is used in library scan, so we should check first for a ignore file and use that here as well
            var matcher = new GlobMatcher();
            foreach (var pattern in library.LibraryExcludePatterns.Where(p => !string.IsNullOrEmpty(p.Pattern)))
            {
                matcher.AddExclude(pattern.Pattern);
            }

            var directories = _directoryService.GetDirectories(folderPath, matcher).ToList();
            foreach (var directory in directories)
            {
                // Since this is a loop, we need a list return
                normalizedPath = Parser.Parser.NormalizePath(directory);
                if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedPath, forceCheck))
                {
                    result.Add(new ScanResult()
                    {
                        Files = ArraySegment<string>.Empty,
                        Folder = directory,
                        LibraryRoot = folderPath,
                        HasChanged = false
                    });

                    //await folderAction(new List<string>(), directory, folderPath);
                }
                else
                {
                    // For a scan, this is doing everything in the directory loop before the folder Action is called...which leads to no progress indication
                    //await folderAction(_directoryService.ScanFiles(directory, fileExtensions, matcher), directory, folderPath);
                    result.Add(new ScanResult()
                    {
                        Files = _directoryService.ScanFiles(directory, fileExtensions, matcher),
                        Folder = directory,
                        LibraryRoot = folderPath,
                        HasChanged = true
                    });
                }
            }

            return result;
        }

        normalizedPath = Parser.Parser.NormalizePath(folderPath);
        if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedPath, forceCheck))
        {
            // await folderAction(new List<string>(), folderPath, folderPath);
            // return;
            result.Add(new ScanResult()
            {
                Files = ArraySegment<string>.Empty,
                Folder = folderPath,
                LibraryRoot = folderPath,
                HasChanged = false
            });
        }

        //await folderAction(_directoryService.ScanFiles(folderPath, fileExtensions), folderPath, folderPath);
        result.Add(new ScanResult()
        {
            Files = _directoryService.ScanFiles(folderPath, fileExtensions),
            Folder = folderPath,
            LibraryRoot = folderPath,
            HasChanged = true
        });

        return result;
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
    /// <param name="processSeriesInfos">Action which returns if the folder was skipped and the infos from said folder</param>
    /// <param name="forceCheck">Defaults to false</param>
    /// <returns></returns>
    public async Task ScanLibrariesForSeries(Library library,
        IEnumerable<string> folders, bool isLibraryScan,
        IDictionary<string, IList<SeriesModified>> seriesPaths, Func<Tuple<bool, IList<ParserInfo>>, Task>? processSeriesInfos, bool forceCheck = false)
    {
        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Starting", library.Name, ProgressEventType.Started));

        var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
        foreach (var folderPath in folders)
        {
            try
            {
                // TODO: This should return an object with the information IList<string> files, string folder, string libraryRoot
                //await ProcessFiles(folderPath, isLibraryScan, seriesPaths, ProcessFolder, library, forceCheck);
                var scanResults = ProcessFiles(folderPath, isLibraryScan, seriesPaths, library, forceCheck);

                foreach (var scanResult in scanResults)
                {
                    await ProcessScanResult(scanResult); // scanResult is updated

                    // We now have all the parsed infos from the scan result, perform any merging that is necessary and post processing steps

                    MergeLocalizedSeriesWithSeries(scanResult.ParserInfos);

                    foreach (var info in scanResult.ParserInfos)
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

                    // I think we can do a bit of work here and streamline this to still use scanResult
                    foreach (var series in scannedSeries.Keys)
                    {
                        if (scannedSeries[series].Count <= 0 || processSeriesInfos == null) continue;

                        UpdateSortOrder(scannedSeries, series);

                        // This should be a return statement I think and we should invoke the actual series work on a background thread
                        await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(!scanResult.HasChanged, scannedSeries[series]));
                    }


                    // if (isLibraryScan)
                    // {
                    //     TrackSeriesFilesForLibraryScan(scanResult);
                    // }
                    // else
                    // {
                    //     TrackSeriesFilesForSeriesScan(scanResult);
                    // }

                }

            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "[ScannerService] The directory '{FolderPath}' does not exist", folderPath);
            }
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("File Scan Done", library.Name, ProgressEventType.Ended));

        async Task ProcessScanResult(ScanResult result)
        {
            // If the folder hasn't changed, generate fake ParserInfos for the Series that were in that folder.
            if (!result.HasChanged)
            {
                var normalizedFolder = Parser.Parser.NormalizePath(result.Folder);
                result.ParserInfos = seriesPaths[normalizedFolder].Select(fp => new ParserInfo()
                {
                    Series = fp.SeriesName,
                    Format = fp.Format,
                }).ToList();


                // ScannerService.TrackFiles is called, it tracks information about the Series like that we've visited it so it doesn't get removed, then the processing occurs


                // if (processSeriesInfos != null)
                //     await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(true, parsedInfos));
                _logger.LogDebug("[ScannerService] Skipped File Scan for {Folder} as it hasn't changed since last scan", normalizedFolder);
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.FileScanProgressEvent("Skipped " + normalizedFolder, library.Name, ProgressEventType.Updated));
                return;
            }

            var files = result.Files;
            var folder = result.Folder;
            var libraryRoot = result.LibraryRoot;

            // When processing files for a folder and we do enter, we need to parse the information and combine parser infos
            // NOTE: We might want to move the merge step later in the process, like return and combine.
            _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, folder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent($"{files.Count} files in {folder}", library.Name, ProgressEventType.Updated));
            if (files.Count == 0)
            {
                _logger.LogInformation("[ScannerService] {Folder} is empty, no longer in this location, or has no file types that match Library File Types", folder);
                result.ParserInfos = ArraySegment<ParserInfo>.Empty;
                return;
            }

            // Multiple Series can exist within a folder. We should instead put these infos on the result and perform merging above
            //var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
            IList<ParserInfo> infos = files
                .Select(file => _readingItemService.ParseFile(file, folder, libraryRoot, library.Type))
                .Where(info => info != null)
                .ToList()!;

            result.ParserInfos = infos;
            return;


            // MergeLocalizedSeriesWithSeries(infos);
            //
            // foreach (var info in infos)
            // {
            //     try
            //     {
            //         TrackSeries(scannedSeries, info);
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogError(ex,
            //             "[ScannerService] There was an exception that occurred during tracking {FilePath}. Skipping this file",
            //             info?.FullFilePath);
            //     }
            // }
            //
            // foreach (var series in scannedSeries.Keys)
            // {
            //     if (scannedSeries[series].Count <= 0 || processSeriesInfos == null) continue;
            //
            //     UpdateSortOrder(scannedSeries, series);
            //
            //     // This should be a return statement I think and we should invoke the actual series work on a background thread
            //     await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(false, scannedSeries[series]));
            // }




        }

        async Task ProcessFolder(IList<string> files, string folder, string libraryRoot)
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
                    MessageFactory.FileScanProgressEvent("Skipped " + normalizedFolder, library.Name, ProgressEventType.Updated));
                return;
            }

            _logger.LogDebug("[ScannerService] Found {Count} files for {Folder}", files.Count, folder);
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.FileScanProgressEvent($"{files.Count} files in {folder}", library.Name, ProgressEventType.Updated));
            if (files.Count == 0)
            {
                _logger.LogInformation("[ScannerService] {Folder} is empty, no longer in this location, or has no file types that match Library File Types", folder);
                return;
            }

            var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
            var infos = files
                .Select(file => _readingItemService.ParseFile(file, folder, libraryRoot, library.Type))
                .Where(info => info != null)
                .ToList();


            MergeLocalizedSeriesWithSeries(infos);

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
                if (scannedSeries[series].Count <= 0 || processSeriesInfos == null) continue;

                UpdateSortOrder(scannedSeries, series);

                // This should be a return statement I think and we should invoke the actual series work on a background thread
                await processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(false, scannedSeries[series]));
            }
        }
    }

    // private void TrackSeriesFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
    // {
    //     // TODO: Make this a record
    //     var skippedScan = parsedInfo.Item1;
    //     var parsedFiles = parsedInfo.Item2;
    //     if (parsedFiles.Count == 0) return;
    //
    //     var foundParsedSeries = new ParsedSeries()
    //     {
    //         Name = parsedFiles[0].Series,
    //         NormalizedName = parsedFiles[0].Series.ToNormalized(),
    //         Format = parsedFiles[0].Format
    //     };
    //
    //     // For Scan Series, we need to filter out anything that isn't our Series
    //     if (!foundParsedSeries.NormalizedName.Equals(series.NormalizedName) && !foundParsedSeries.NormalizedName.Equals(series.OriginalName?.ToNormalized()))
    //     {
    //         return;
    //     }
    //
    //     parsedSeries.Add(foundParsedSeries, parsedFiles);
    // }

    private void UpdateSortOrder(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParsedSeries series)
    {
        try
        {
            // Set the Sort order per Volume
            var volumes = scannedSeries[series].GroupBy(info => info.Volumes);
            foreach (var volume in volumes)
            {
                var infos = scannedSeries[series].Where(info => info.Volumes == volume.Key).ToList();
                IList<ParserInfo> chapters;
                var specialTreatment = infos.TrueForAll(info => info.IsSpecial);

                if (specialTreatment)
                {
                    chapters = infos
                        .OrderBy(info => info.SpecialIndex)
                        .ToList();
                }
                else
                {
                    chapters = infos
                        .OrderByNatural(info => info.Chapters)
                        .ToList();
                }


                var counter = 0f;
                var prevIssue = string.Empty;
                foreach (var chapter in chapters)
                {
                    if (float.TryParse(chapter.Chapters, out var parsedChapter))
                    {
                        counter = parsedChapter;
                        if (!string.IsNullOrEmpty(prevIssue) && parsedChapter.Is(float.Parse(prevIssue)))
                        {
                            // Bump by 0.1
                            counter += 0.1f;
                        }
                        chapter.IssueOrder = counter;
                        prevIssue = $"{parsedChapter}";
                    }
                    else
                    {
                        // I need to bump by 0.1f as if the prevIssue matches counter
                        if (!string.IsNullOrEmpty(prevIssue) && prevIssue == counter + "")
                        {
                            // Bump by 0.1
                            counter += 0.1f;
                        }
                        chapter.IssueOrder = counter;
                        counter++;
                        prevIssue = chapter.Chapters;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue setting IssueOrder");
        }
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
    private void MergeLocalizedSeriesWithSeries(IList<ParserInfo> infos)
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
            nonLocalizedSeries = nonLocalizedSeriesFound[0];
        }
        else
        {
            // There can be a case where there are multiple series in a folder that causes merging.
            if (nonLocalizedSeriesFound.Count > 2)
            {
                _logger.LogError("[ScannerService] There are multiple series within one folder that contain localized series. This will cause them to group incorrectly. Please separate series into their own dedicated folder or ensure there is only 2 potential series (localized and series):  {LocalizedSeries}", string.Join(", ", nonLocalizedSeriesFound));
            }
            nonLocalizedSeries = nonLocalizedSeriesFound.Find(s => !s.Equals(localizedSeries));
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
