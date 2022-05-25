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
            var normalizedLocalizedSeries = Parser.Parser.Normalize(info.LocalizedSeries);
            var existingKey = _scannedSeries.Keys.FirstOrDefault(ps =>
                ps.Format == info.Format && (ps.NormalizedName == normalizedSeries
                                             || ps.NormalizedName == normalizedLocalizedSeries));
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
            // We use FirstOrDefault because this was introduced late in development and users might have 2 series with both names
            var existingName =
                _scannedSeries.FirstOrDefault(p =>
                        (Parser.Parser.Normalize(p.Key.NormalizedName) == normalizedSeries ||
                         Parser.Parser.Normalize(p.Key.NormalizedName) == normalizedLocalSeries) && p.Key.Format == info.Format)
                .Key;
            if (existingName != null && !string.IsNullOrEmpty(existingName.Name))
            {
                return existingName.Name;
            }

            return info.Series;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="libraryType">Type of library. Used for selecting the correct file extensions to search for and parsing files</param>
        /// <param name="folders">The folders to scan. By default, this should be library.Folders, however it can be overwritten to restrict folders</param>
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
