using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Interfaces.Services;
using API.Parser;
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
        private readonly IBookService _bookService;
        private readonly ILogger _logger;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;

        /// <summary>
        /// An instance of a pipeline for processing files and returning a Map of Series -> ParserInfos.
        /// Each instance is separate from other threads, allowing for no cross over.
        /// </summary>
        /// <param name="bookService"></param>
        /// <param name="logger"></param>
        public ParseScannedFiles(IBookService bookService, ILogger logger, IArchiveService archiveService,
            IDirectoryService directoryService)
        {
            _bookService = bookService;
            _logger = logger;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _scannedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
        }

        /// <summary>
        /// Gets the list of parserInfos given a Series. If the series does not exist within, return empty list.
        /// </summary>
        /// <param name="parsedSeries"></param>
        /// <param name="series"></param>
        /// <returns></returns>
        public static IList<ParserInfo> GetInfosByName(Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Series series)
        {
            var existingKey = parsedSeries.Keys.FirstOrDefault(ps =>
                ps.Format == series.Format && ps.NormalizedName.Equals(Parser.Parser.Normalize(series.OriginalName)));

            return existingKey != null ? parsedSeries[existingKey] : new List<ParserInfo>();
        }

        private ComicInfo GetComicInfo(string path)
        {
            if (Parser.Parser.IsEpub(path))
            {
                return _bookService.GetComicInfo(path);
            }

            if (Parser.Parser.IsComicInfoExtension(path))
            {
                return _archiveService.GetComicInfo(path);
            }
            return null;
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
            ParserInfo info;

            if (Parser.Parser.IsEpub(path))
            {
                info = _bookService.ParseInfo(path);
            }
            else
            {
                info = Parser.Parser.Parse(path, rootPath, type);
            }

            // If we couldn't match, log. But don't log if the file parses as a cover image
            if (info == null)
            {
                if (!(Parser.Parser.IsImage(path) && Parser.Parser.IsCoverImage(path)))
                {
                    _logger.LogWarning("[Scanner] Could not parse series from {Path}", path);
                }
                return;
            }

            if (Parser.Parser.IsEpub(path) && Parser.Parser.ParseVolume(info.Series) != Parser.Parser.DefaultVolume)
            {
                info = Parser.Parser.Parse(path, rootPath, type);
                var info2 = _bookService.ParseInfo(path);
                info.Merge(info2);
            }

            // TODO: Think about doing this before the Fallback code to speed up
            info.ComicInfo = GetComicInfo(path);
            if (info.ComicInfo != null)
            {
                var sw = Stopwatch.StartNew();

                if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
                {
                    info.Volumes = info.ComicInfo.Volume;
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Series))
                {
                    info.Series = info.ComicInfo.Series;
                }
                if (!string.IsNullOrEmpty(info.ComicInfo.Number))
                {
                    info.Chapters = info.ComicInfo.Number;
                }

                _logger.LogDebug("ComicInfo read added {Time} ms to processing", sw.ElapsedMilliseconds);
            }

            TrackSeries(info);
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

            var existingKey = _scannedSeries.Keys.FirstOrDefault(ps =>
                ps.Format == info.Format && ps.NormalizedName == Parser.Parser.Normalize(info.Series));
            existingKey ??= new ParsedSeries()
            {
                Format = info.Format,
                Name = info.Series,
                NormalizedName = Parser.Parser.Normalize(info.Series)
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
        /// <returns></returns>
        public string MergeName(ParserInfo info)
        {
            var normalizedSeries = Parser.Parser.Normalize(info.Series);
            var existingName =
                _scannedSeries.SingleOrDefault(p => Parser.Parser.Normalize(p.Key.NormalizedName) == normalizedSeries && p.Key.Format == info.Format)
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
        /// <param name="totalFiles">Total files scanned</param>
        /// <param name="scanElapsedTime">Time it took to scan and parse files</param>
        /// <returns></returns>
        public Dictionary<ParsedSeries, List<ParserInfo>> ScanLibrariesForSeries(LibraryType libraryType, IEnumerable<string> folders, out int totalFiles,
            out long scanElapsedTime)
        {
            var sw = Stopwatch.StartNew();
            totalFiles = 0;
            var searchPattern = Parser.Parser.SupportedExtensions;
            foreach (var folderPath in folders)
            {
                try
                {
                    totalFiles += _directoryService.TraverseTreeParallelForEach(folderPath, (f) =>
                    {
                        try
                        {
                            ProcessFile(f, folderPath, libraryType);
                        }
                        catch (FileNotFoundException exception)
                        {
                            _logger.LogError(exception, "The file {Filename} could not be found", f);
                        }
                    }, searchPattern, _logger);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "The directory '{FolderPath}' does not exist", folderPath);
                }
            }

            scanElapsedTime = sw.ElapsedMilliseconds;
            _logger.LogInformation("Scanned {TotalFiles} files in {ElapsedScanTime} milliseconds", totalFiles,
                scanElapsedTime);

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
