using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Parser;
using API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner
{
    public class ParsedSeries
    {
        public string Name { get; init; }
        public string NormalizedName { get; init; }
        public MangaFormat Format { get; init; }
    }


    public class ParseScannedFiles
    {
        private readonly ConcurrentDictionary<ParsedSeries, List<ParserInfo>> _scannedSeries;
        private readonly ILogger _logger;
        private readonly IDirectoryService _directoryService;
        private readonly IReadingItemService _readingItemService;
        private readonly IEventHub _eventHub;
        private readonly DefaultParser _defaultParser;

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
            _scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
            _defaultParser = new DefaultParser(_directoryService);
            _eventHub = eventHub;
        }

        /// <summary>
        /// Gets the list of all parserInfos given a Series (Will match on Name, LocalizedName, OriginalName). If the series does not exist within, return empty list.
        /// </summary>
        /// <param name="parsedSeries"></param>
        /// <param name="series"></param>
        /// <returns></returns>
        public static IList<ParserInfo> GetInfosByName(Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Series series)
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
        /// <param name="folderPath">A library folder or series folder</param>
        /// <param name="folderAction"></param>
        public void ProcessFiles(string folderPath, bool isLibraryFolder, Action<IEnumerable<string>> folderAction)
        {
            if (isLibraryFolder)
            {
                var directories = _directoryService.GetDirectories(folderPath).ToList();

                foreach (var directory in directories)
                {
                    folderAction(ScanFiles(directory));
                }

                // if (directories.Count() == 0)
                // {
                //     folderAction(ScanFiles(folderPath));
                // }
            }
            else
            {
                folderAction(ScanFiles(folderPath));
            }
        }


        public IEnumerable<string> ScanFiles(string folderPath, Matcher? matcher = null)
        {
            var files = new List<string>();
            if (!_directoryService.Exists(folderPath)) return files;

            var potentialIgnoreFile = _directoryService.FileSystem.Path.Join(folderPath, ".kavitaignore");
            if (matcher == null)
            {
                matcher = CreateIgnoreMatcher(potentialIgnoreFile);
            }


            foreach (var directory in _directoryService.GetDirectories(folderPath))
            {
                files.AddRange(ScanFiles(directory, matcher));
            }


            // Get the matcher from either ignore or global (default setup)
            if (matcher == null)
            {
                files.AddRange(_directoryService.GetFilesWithCertainExtensions(folderPath, Parser.Parser.SupportedExtensions));
            }
            else
            {
                // Matching only works on files
                files.AddRange(matcher.GetResultsInFullPath(folderPath).Where(f => f.EndsWith(Parser.Parser.SupportedExtensions)));
            }

            return files;
        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>See https://www.digitalocean.com/community/tools/glob for testing</remarks>
        /// <param name="ignoreFile"></param>
        /// <returns></returns>
        private Matcher CreateIgnoreMatcher(string ignoreFile)
        {
            Matcher matcher = new();

            if (!_directoryService.FileSystem.File.Exists(ignoreFile))
            {
                return null;
            }

            // Read file in and add each line to Matcher
            matcher.AddInclude("**/*"); // All files, nested or otherwise
            matcher.AddExcludePatterns(_directoryService.FileSystem.File.ReadAllLines(ignoreFile));
            return matcher;
        }

        /// <summary>
        /// Processes files found during a library scan.
        /// Populates a collection of <see cref="ParserInfo"/> for DB updates later.
        /// </summary>
        /// <param name="path">Path of a file</param>
        /// <param name="rootPath"></param>
        /// <param name="type">Library type to determine parsing to perform</param>
        private void ProcessFile(string path, string rootPath, LibraryType type)
        {
            var info = _readingItemService.Parse(path, rootPath, type);
            if (info == null)
            {
                // If the file is an image and literally a cover image, skip processing.
                if (!(Parser.Parser.IsImage(path) && Parser.Parser.IsCoverImage(path)))
                {
                    _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
                }
                return;
            }


            // This catches when original library type is Manga/Comic and when parsing with non
            if (Parser.Parser.IsEpub(path) && Parser.Parser.ParseVolume(info.Series) != Parser.Parser.DefaultVolume) // Shouldn't this be info.Volume != DefaultVolume?
            {
                info = _defaultParser.Parse(path, rootPath, LibraryType.Book);
                var info2 = _readingItemService.Parse(path, rootPath, type);
                info.Merge(info2);
            }

            info.ComicInfo = _readingItemService.GetComicInfo(path);
            if (info.ComicInfo != null)
            {
                if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
                {
                    info.Volumes = info.ComicInfo.Volume;
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Series))
                {
                    info.Series = info.ComicInfo.Series.Trim();
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Number))
                {
                    info.Chapters = info.ComicInfo.Number;
                }

                // Patch is SeriesSort from ComicInfo
                if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
                {
                    info.SeriesSort = info.ComicInfo.TitleSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Parser.Parser.HasComicInfoSpecial(info.ComicInfo.Format))
                {
                    info.IsSpecial = true;
                    info.Chapters = Parser.Parser.DefaultChapter;
                    info.Volumes = Parser.Parser.DefaultVolume;
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
                {
                    info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
                {
                    info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
                }
            }

            try
            {
                TrackSeries(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception that occurred during tracking {FilePath}. Skipping this file", info.FullFilePath);
            }
        }


        private ParserInfo? ProcessFileNoTrack(string path, string rootPath, LibraryType type)
        {
            var info = _readingItemService.Parse(path, rootPath, type);
            if (info == null)
            {
                // If the file is an image and literally a cover image, skip processing.
                if (!(Parser.Parser.IsImage(path) && Parser.Parser.IsCoverImage(path)))
                {
                    _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
                }
                return null;
            }


            // This catches when original library type is Manga/Comic and when parsing with non
            if (Parser.Parser.IsEpub(path) && Parser.Parser.ParseVolume(info.Series) != Parser.Parser.DefaultVolume) // Shouldn't this be info.Volume != DefaultVolume?
            {
                info = _defaultParser.Parse(path, rootPath, LibraryType.Book);
                var info2 = _readingItemService.Parse(path, rootPath, type);
                info.Merge(info2);
            }

            info.ComicInfo = _readingItemService.GetComicInfo(path);
            if (info.ComicInfo != null)
            {
                if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
                {
                    info.Volumes = info.ComicInfo.Volume;
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Series))
                {
                    info.Series = info.ComicInfo.Series.Trim();
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Number))
                {
                    info.Chapters = info.ComicInfo.Number;
                }

                // Patch is SeriesSort from ComicInfo
                if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
                {
                    info.SeriesSort = info.ComicInfo.TitleSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Parser.Parser.HasComicInfoSpecial(info.ComicInfo.Format))
                {
                    info.IsSpecial = true;
                    info.Chapters = Parser.Parser.DefaultChapter;
                    info.Volumes = Parser.Parser.DefaultVolume;
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
                {
                    info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
                {
                    info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
                }
            }

            return info;
        }

        private ParserInfo ProcessFile2(string path, string rootPath, LibraryType type)
        {
            var info = _readingItemService.Parse(path, rootPath, type);
            if (info == null)
            {
                // If the file is an image and literally a cover image, skip processing.
                if (!(Parser.Parser.IsImage(path) && Parser.Parser.IsCoverImage(path)))
                {
                    _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
                }
                return null;
            }


            // This catches when original library type is Manga/Comic and when parsing with non
            if (Parser.Parser.IsEpub(path) && Parser.Parser.ParseVolume(info.Series) != Parser.Parser.DefaultVolume) // Shouldn't this be info.Volume != DefaultVolume?
            {
                info = _defaultParser.Parse(path, rootPath, LibraryType.Book);
                var info2 = _readingItemService.Parse(path, rootPath, type);
                info.Merge(info2);
            }

            info.ComicInfo = _readingItemService.GetComicInfo(path);
            if (info.ComicInfo != null)
            {
                if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
                {
                    info.Volumes = info.ComicInfo.Volume;
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Series))
                {
                    info.Series = info.ComicInfo.Series.Trim();
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Number))
                {
                    info.Chapters = info.ComicInfo.Number;
                }

                // Patch is SeriesSort from ComicInfo
                if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
                {
                    info.SeriesSort = info.ComicInfo.TitleSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Parser.Parser.HasComicInfoSpecial(info.ComicInfo.Format))
                {
                    info.IsSpecial = true;
                    info.Chapters = Parser.Parser.DefaultChapter;
                    info.Volumes = Parser.Parser.DefaultVolume;
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
                {
                    info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
                }

                if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
                {
                    info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
                }
            }

            return info;
        }


        /// <summary>
        /// Attempts to either add a new instance of a show mapping to the _scannedSeries bag or adds to an existing.
        /// This will check if the name matches an existing series name (multiple fields) <see cref="MergeName"/>
        /// </summary>
        /// <param name="info"></param>
        private void TrackSeries(ParserInfo info)
        {
            if (info.Series == string.Empty) return;

            // Check if normalized info.Series already exists and if so, update info to use that name instead
            info.Series = MergeName(info);

            var normalizedSeries = Parser.Parser.Normalize(info.Series);
            var normalizedSortSeries = Parser.Parser.Normalize(info.SeriesSort);
            var normalizedLocalizedSeries = Parser.Parser.Normalize(info.LocalizedSeries);

            try
            {
                var existingKey = _scannedSeries.Keys.SingleOrDefault(ps =>
                    ps.Format == info.Format && (ps.NormalizedName.Equals(normalizedSeries)
                                                 || ps.NormalizedName.Equals(normalizedLocalizedSeries)
                                                 || ps.NormalizedName.Equals(normalizedSortSeries)));
                existingKey ??= new ParsedSeries()
                {
                    Format = info.Format,
                    Name = info.Series,
                    NormalizedName = normalizedSeries
                };

                _scannedSeries.AddOrUpdate(existingKey, new List<ParserInfo>() {info}, (_, oldValue) =>
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
                foreach (var seriesKey in _scannedSeries.Keys.Where(ps =>
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
        public string MergeName(ParserInfo info)
        {
            var normalizedSeries = Parser.Parser.Normalize(info.Series);
            var normalizedLocalSeries = Parser.Parser.Normalize(info.LocalizedSeries);

            try
            {
                var existingName =
                    _scannedSeries.SingleOrDefault(p =>
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
                var values = _scannedSeries.Where(p =>
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
        ///
        /// </summary>
        /// <param name="libraryType">Type of library. Used for selecting the correct file extensions to search for and parsing files</param>
        /// <param name="folders">The folders to scan. By default, this should be library.Folders, however it can be overwritten to restrict folders</param>
        /// <param name="libraryName">Name of the Library</param>
        /// <returns></returns>
        public async Task<Dictionary<ParsedSeries, List<ParserInfo>>> ScanLibrariesForSeries(LibraryType libraryType, IEnumerable<string> folders, string libraryName)
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("", libraryName, ProgressEventType.Started));
            foreach (var folderPath in folders)
            {
                try
                {
                    async void Action(string f)
                    {
                        try
                        {
                            ProcessFile(f, folderPath, libraryType);
                            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(f, libraryName, ProgressEventType.Updated));
                        }
                        catch (FileNotFoundException exception)
                        {
                            _logger.LogError(exception, "The file {Filename} could not be found", f);
                        }
                    }

                    _directoryService.TraverseTreeParallelForEach(folderPath, Action, Parser.Parser.SupportedExtensions, _logger);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath);
                }
            }

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("", libraryName, ProgressEventType.Ended));

            // Can't I fix the localizedTitle and title duplication here by doing another loop through our dictionary

            return SeriesWithInfos();
        }

        /// <summary>
        /// This is a new version which will process series by folder groups.
        /// </summary>
        /// <param name="libraryType"></param>
        /// <param name="folders"></param>
        /// <param name="libraryName"></param>
        /// <returns></returns>
        public async Task<Dictionary<ParsedSeries, List<ParserInfo>>> ScanLibrariesForSeries2(LibraryType libraryType, IEnumerable<string> folders, string libraryName, bool isLibraryScan)
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("", libraryName, ProgressEventType.Started));

            foreach (var folderPath in folders)
            {
                try
                {
                    var infos = new List<ParserInfo>();
                    ProcessFiles(folderPath, isLibraryScan, (files) =>
                    {
                        foreach (var file in files)
                        {
                            try
                            {
                                var info = ProcessFile2(file, folderPath, libraryType);
                                if (info != null) infos.Add(info);

                            }
                            catch (FileNotFoundException exception)
                            {
                                _logger.LogError(exception, "The file {Filename} could not be found", file);
                            }
                        }
                    });
                    await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent(folderPath, libraryName, ProgressEventType.Updated));
                    //var files = ScanFiles(folderPath); // This has the same problem that whole library must be scanned before we can move on

                    // Group into a series



                    // See if we can combine any series with others that have a localizedSeries
                    var hasLocalizedSeries = infos.Any(i => !string.IsNullOrEmpty(i.LocalizedSeries));
                    if (hasLocalizedSeries)
                    {
                        var localizedSeries = infos.Select(i => i.LocalizedSeries).Distinct()
                            .FirstOrDefault(i => !string.IsNullOrEmpty(i));
                        if (!string.IsNullOrEmpty(localizedSeries))
                        {
                            var nonLocalizedSeries = infos.Select(i => i.Series).Distinct()
                                .FirstOrDefault(series => !series.Equals(localizedSeries));

                            var normalizedNonLocalizedSeries = Parser.Parser.Normalize(nonLocalizedSeries);
                            foreach (var infoNeedingMapping in infos.Where(i => !Parser.Parser.Normalize(i.Series).Equals(normalizedNonLocalizedSeries)))
                            {
                                infoNeedingMapping.Series = nonLocalizedSeries;
                            }
                        }
                    }


                    foreach (var info in infos)
                    {
                        try
                        {
                            TrackSeries(info);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "There was an exception that occurred during tracking {FilePath}. Skipping this file", info.FullFilePath);
                        }
                    }



                    // var seriesNames = infos.Select(i => i.Series).ToList();
                    // var localizedNames = infos.Select(i => i.LocalizedSeries).ToList();
                    // if (seriesNames.All(s => s.Equals(seriesNames[0])))
                    // {
                    //
                    // }
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath);
                }
            }

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, MessageFactory.FileScanProgressEvent("", libraryName, ProgressEventType.Ended));

            return SeriesWithInfos();
        }

        /// <summary>
        /// Returns any series where there were parsed infos
        /// </summary>
        /// <returns></returns>
        private Dictionary<ParsedSeries, List<ParserInfo>> SeriesWithInfos()
        {
            var filtered = _scannedSeries.Where(kvp => kvp.Value.Count > 0);
            var series = filtered.ToDictionary(v => v.Key, v => v.Value);
            return series;
        }
    }
}
