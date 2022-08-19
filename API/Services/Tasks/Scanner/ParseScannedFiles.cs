using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Parser;
using API.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner
{
    public class ParsedSeries
    {
        /// <summary>
        /// Name of the Series
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Normalized Name of the Series
        /// </summary>
        public string NormalizedName { get; init; }
        /// <summary>
        /// Format of the Series
        /// </summary>
        public MangaFormat Format { get; init; }
    }

    public enum Modified
    {
        Modified = 1,
        NotModified = 2
    }

    public class SeriesModified
    {
        public string FolderPath { get; set; }
        public string SeriesName { get; set; }
        public DateTime LastScanned { get; set; }
        public MangaFormat Format { get; set; }
    }


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
        /// Gets the list of all parserInfos given a Series (Will match on Name, LocalizedName, OriginalName). If the series does not exist within, return empty list.
        /// </summary>
        /// <param name="parsedSeries"></param>
        /// <param name="series"></param>
        /// <returns></returns>
        public static IList<ParserInfo> GetInfosByName(Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries, Series series)
        {
            var allKeys = parsedSeries.Keys.Where(ps =>
                SeriesHelper.FindSeries(series, ps));

            var infos = new List<ParserInfo>();
            foreach (var key in allKeys)
            {
                infos.AddRange(parsedSeries[key]);
            }

            return infos;
        }


        /// <summary>
        /// This will Scan all files in a folder path. For each folder within the folderPath, FolderAction will be invoked for all files contained
        /// </summary>
        /// <param name="scanDirectoryByDirectory">Scan directory by directory and for each, call folderAction</param>
        /// <param name="folderPath">A library folder or series folder</param>
        /// <param name="folderAction">A callback async Task to be called once all files for each folder path are found</param>
        /// <param name="forceCheck">If we should bypass any folder last write time checks on the scan and force I/O</param>
        public async Task ProcessFiles(string folderPath, bool scanDirectoryByDirectory,
            IDictionary<string, IList<SeriesModified>> seriesPaths, Func<IList<string>, string,Task> folderAction, bool forceCheck = false)
        {
            string normalizedPath;
            if (scanDirectoryByDirectory)
            {
                var directories = _directoryService.GetDirectories(folderPath).ToList();

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
                        await folderAction(_directoryService.ScanFiles(directory), directory);
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
            await folderAction(_directoryService.ScanFiles(folderPath), folderPath);
        }


        /// <summary>
        /// Attempts to either add a new instance of a show mapping to the _scannedSeries bag or adds to an existing.
        /// This will check if the name matches an existing series name (multiple fields) <see cref="MergeName"/>
        /// </summary>
        /// <param name="scannedSeries">A localized list of a series' parsed infos</param>
        /// <param name="info"></param>
        private void TrackSeries(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParserInfo info)
        {
            if (info.Series == string.Empty) return;

            // Check if normalized info.Series already exists and if so, update info to use that name instead
            info.Series = MergeName(scannedSeries, info);

            var normalizedSeries = Parser.Parser.Normalize(info.Series);
            var normalizedSortSeries = Parser.Parser.Normalize(info.SeriesSort);
            var normalizedLocalizedSeries = Parser.Parser.Normalize(info.LocalizedSeries);

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
                _logger.LogCritical(ex, "{SeriesName} matches against multiple series in the parsed series. This indicates a critical kavita issue. Key will be skipped", info.Series);
                foreach (var seriesKey in scannedSeries.Keys.Where(ps =>
                             ps.Format == info.Format && (ps.NormalizedName.Equals(normalizedSeries)
                                                          || ps.NormalizedName.Equals(normalizedLocalizedSeries)
                                                          || ps.NormalizedName.Equals(normalizedSortSeries))))
                {
                    _logger.LogCritical("Matches: {SeriesName} matches on {SeriesKey}", info.Series, seriesKey.Name);
                }
            }
        }


        /// <summary>
        /// Using a normalized name from the passed ParserInfo, this checks against all found series so far and if an existing one exists with
        /// same normalized name, it merges into the existing one. This is important as some manga may have a slight difference with punctuation or capitalization.
        /// </summary>
        /// <param name="info"></param>
        /// <returns>Series Name to group this info into</returns>
        public string MergeName(ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries, ParserInfo info)
        {
            var normalizedSeries = Parser.Parser.Normalize(info.Series);
            var normalizedLocalSeries = Parser.Parser.Normalize(info.LocalizedSeries);

            try
            {
                var existingName =
                    scannedSeries.SingleOrDefault(p =>
                            (Parser.Parser.Normalize(p.Key.NormalizedName).Equals(normalizedSeries) ||
                             Parser.Parser.Normalize(p.Key.NormalizedName).Equals(normalizedLocalSeries)) &&
                            p.Key.Format == info.Format)
                        .Key;

                if (existingName != null && !string.IsNullOrEmpty(existingName.Name))
                {
                    return existingName.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Multiple series detected for {SeriesName} ({File})! This is critical to fix! There should only be 1", info.Series, info.FullFilePath);
                var values = scannedSeries.Where(p =>
                    (Parser.Parser.Normalize(p.Key.NormalizedName) == normalizedSeries ||
                     Parser.Parser.Normalize(p.Key.NormalizedName) == normalizedLocalSeries) &&
                    p.Key.Format == info.Format);
                foreach (var pair in values)
                {
                    _logger.LogCritical("Duplicate Series in DB matches with {SeriesName}: {DuplicateName}", info.Series, pair.Key.Name);
                }

            }

            return info.Series;
        }


        /// <summary>
        /// This is a new version which will process series by folder groups.
        /// </summary>
        /// <param name="libraryType"></param>
        /// <param name="folders"></param>
        /// <param name="libraryName"></param>
        /// <returns></returns>
        public async Task ScanLibrariesForSeries(LibraryType libraryType,
            IEnumerable<string> folders, string libraryName, bool isLibraryScan,
            IDictionary<string, IList<SeriesModified>> seriesPaths, Action<Tuple<bool, IList<ParserInfo>>> processSeriesInfos, bool forceCheck = false)
        {

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("Starting file scan", libraryName, ProgressEventType.Started));

            foreach (var folderPath in folders)
            {
                try
                {
                    await ProcessFiles(folderPath, isLibraryScan, seriesPaths, async (files, folder) =>
                    {
                        var normalizedFolder = Parser.Parser.NormalizePath(folder);
                        if (HasSeriesFolderNotChangedSinceLastScan(seriesPaths, normalizedFolder, forceCheck))
                        {
                            var parsedInfos = seriesPaths[normalizedFolder].Select(fp => new ParserInfo()
                            {
                                Series = fp.SeriesName,
                                Format = fp.Format,
                            }).ToList();
                            processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(true, parsedInfos));
                            _logger.LogDebug("Skipped File Scan for {Folder} as it hasn't changed since last scan", folder);
                            return;
                        }
                        _logger.LogDebug("Found {Count} files for {Folder}", files.Count, folder);
                        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(folderPath, libraryName, ProgressEventType.Updated));
                        var scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
                        var infos = files.Select(file => _readingItemService.ParseFile(file, folderPath, libraryType)).Where(info => info != null).ToList();


                        MergeLocalizedSeriesWithSeries(infos);

                        foreach (var info in infos)
                        {
                            try
                            {
                                TrackSeries(scannedSeries, info);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "There was an exception that occurred during tracking {FilePath}. Skipping this file", info.FullFilePath);
                            }
                        }

                        // It would be really cool if we can emit an event when a folder hasn't been changed so we don't parse everything, but the first item to ensure we don't delete it
                        // Otherwise, we can do a last step in the DB where we validate all files on disk exist and if not, delete them. (easy but slow)
                        foreach (var series in scannedSeries.Keys)
                        {
                            if (scannedSeries[series].Count > 0 && processSeriesInfos != null)
                            {
                                processSeriesInfos.Invoke(new Tuple<bool, IList<ParserInfo>>(false, scannedSeries[series]));
                            }
                        }
                    }, forceCheck);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath);
                }
            }

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(string.Empty, libraryName, ProgressEventType.Ended));
        }

        private bool HasSeriesFolderNotChangedSinceLastScan(IDictionary<string, IList<SeriesModified>> seriesPaths, string normalizedFolder, bool forceCheck = false)
        {
            if (forceCheck) return false;

            return seriesPaths.ContainsKey(normalizedFolder) && seriesPaths[normalizedFolder].All(f => f.LastScanned.Truncate(TimeSpan.TicksPerMinute) >=
                _directoryService.GetLastWriteTime(normalizedFolder).Truncate(TimeSpan.TicksPerMinute));
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
        private static void MergeLocalizedSeriesWithSeries(IReadOnlyCollection<ParserInfo> infos)
        {
            var hasLocalizedSeries = infos.Any(i => !string.IsNullOrEmpty(i.LocalizedSeries));
            if (!hasLocalizedSeries) return;

            var localizedSeries = infos.Select(i => i.LocalizedSeries).Distinct()
                .FirstOrDefault(i => !string.IsNullOrEmpty(i));
            if (string.IsNullOrEmpty(localizedSeries)) return;

            var nonLocalizedSeries = infos.Select(i => i.Series).Distinct()
                .FirstOrDefault(series => !series.Equals(localizedSeries));

            var normalizedNonLocalizedSeries = Parser.Parser.Normalize(nonLocalizedSeries);
            foreach (var infoNeedingMapping in infos.Where(i =>
                         !Parser.Parser.Normalize(i.Series).Equals(normalizedNonLocalizedSeries)))
            {
                infoNeedingMapping.Series = nonLocalizedSeries;
                infoNeedingMapping.LocalizedSeries = localizedSeries;
            }
        }
    }
}
