using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using API.Interfaces.Services;
using API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MetadataService> _logger;
        private readonly IArchiveService _archiveService;
        private readonly IBookService _bookService;
        private readonly IImageService _imageService;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

        public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger,
            IArchiveService archiveService, IBookService bookService, IImageService imageService, IHubContext<MessageHub> messageHub)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _archiveService = archiveService;
            _bookService = bookService;
            _imageService = imageService;
            _messageHub = messageHub;
        }

        /// <summary>
        /// Determines whether an entity should regenerate cover image.
        /// </summary>
        /// <remarks>If a cover image is locked but the underlying file has been deleted, this will allow regenerating. </remarks>
        /// <param name="coverImage"></param>
        /// <param name="firstFile"></param>
        /// <param name="forceUpdate"></param>
        /// <param name="isCoverLocked"></param>
        /// <param name="coverImageDirectory">Directory where cover images are. Defaults to <see cref="DirectoryService.CoverImageDirectory"/></param>
        /// <returns></returns>
        public static bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, bool forceUpdate = false,
            bool isCoverLocked = false, string coverImageDirectory = null)
        {
            if (string.IsNullOrEmpty(coverImageDirectory))
            {
                coverImageDirectory = DirectoryService.CoverImageDirectory;
            }

            var fileExists = File.Exists(Path.Join(coverImageDirectory, coverImage));
            if (isCoverLocked && fileExists) return false;
            if (forceUpdate) return true;
            return (firstFile != null && firstFile.HasFileBeenModified()) || !HasCoverImage(coverImage, fileExists);
        }


        private static bool HasCoverImage(string coverImage)
        {
            return HasCoverImage(coverImage, File.Exists(coverImage));
        }

        private static bool HasCoverImage(string coverImage, bool fileExists)
        {
            return !string.IsNullOrEmpty(coverImage) && fileExists;
        }

        private string GetCoverImage(MangaFile file, int volumeId, int chapterId)
        {
            file.UpdateLastModified();
            switch (file.Format)
            {
                case MangaFormat.Pdf:
                case MangaFormat.Epub:
                    return _bookService.GetCoverImage(file.FilePath, ImageService.GetChapterFormat(chapterId, volumeId));
                case MangaFormat.Image:
                    var coverImage = _imageService.GetCoverFile(file);
                    return _imageService.GetCoverImage(coverImage, ImageService.GetChapterFormat(chapterId, volumeId));
                case MangaFormat.Archive:
                    return _archiveService.GetCoverImage(file.FilePath, ImageService.GetChapterFormat(chapterId, volumeId));
                default:
                    return string.Empty;
            }

        }

        /// <summary>
        /// Updates the metadata for a Chapter
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public bool UpdateMetadata(Chapter chapter, ChapterMetadata metadata, bool forceUpdate)
        {
            var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
            var updateMade = false;

            if (ShouldUpdateCoverImage(chapter.CoverImage, firstFile, forceUpdate, chapter.CoverImageLocked))
            {
                _logger.LogDebug("[MetadataService] Generating cover image for {File}", firstFile?.FilePath);
                chapter.CoverImage = GetCoverImage(firstFile, chapter.VolumeId, chapter.Id);
                updateMade = true;
            }

            // TODO: Hook in Chapter metadata code
            if (firstFile != null && (forceUpdate || firstFile.HasFileBeenModified()))
            {
                var comicInfo = _archiveService.GetComicInfo(firstFile.FilePath);
                if (comicInfo == null) return false;

                if (!string.IsNullOrEmpty(comicInfo.Title))
                {
                    metadata.Title = comicInfo.Title;
                }

                updateMade = true;
            }


            return updateMade;
        }

        /// <summary>
        /// Updates the metadata for a Volume
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public bool UpdateMetadata(Volume volume, bool forceUpdate)
        {
            // We need to check if Volume coverImage matches first chapters if forceUpdate is false
            if (volume == null || !ShouldUpdateCoverImage(volume.CoverImage, null, forceUpdate)) return false;

            volume.Chapters ??= new List<Chapter>();
            var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).FirstOrDefault();
            if (firstChapter == null) return false;

            volume.CoverImage = firstChapter.CoverImage;
            return true;
        }

        /// <summary>
        /// Updates metadata for Series
        /// </summary>
        /// <param name="series"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public bool UpdateMetadata(Series series, bool forceUpdate)
        {
            var madeUpdate = false;
            if (series == null) return false;

            // NOTE: This will fail if we replace the cover of the first volume on a first scan. Because the series will already have a cover image
            if (ShouldUpdateCoverImage(series.CoverImage, null, forceUpdate, series.CoverImageLocked))
            {
                series.Volumes ??= new List<Volume>();
                var firstCover = series.Volumes.GetCoverImage(series.Format);
                string coverImage = null;
                if (firstCover == null && series.Volumes.Any())
                {
                    // If firstCover is null and one volume, the whole series is Chapters under Vol 0.
                    if (series.Volumes.Count == 1)
                    {
                        coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                            .FirstOrDefault(c => !c.IsSpecial)?.CoverImage;
                        madeUpdate = true;
                    }

                    if (!HasCoverImage(coverImage))
                    {
                        coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                            .FirstOrDefault()?.CoverImage;
                        madeUpdate = true;
                    }
                }
                series.CoverImage = firstCover?.CoverImage ?? coverImage;
            }


            return UpdateSeriesMetadata(series, forceUpdate) || madeUpdate;
        }

        private bool UpdateSeriesMetadata(Series series, bool forceUpdate)
        {
            // NOTE: This can be problematic when the file changes and a summary already exists, but it is likely
            // better to let the user kick off a refresh metadata on an individual Series than having overhead of
            // checking File last write time.
            if (!string.IsNullOrEmpty(series.Summary) && !forceUpdate) return false;

            var isBook = series.Library.Type == LibraryType.Book;
            var firstVolume = series.Volumes.FirstWithChapters(isBook);
            var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

            var firstFile = firstChapter?.Files.FirstOrDefault();
            if (firstFile == null || (!forceUpdate && !firstFile.HasFileBeenModified())) return false;
            if (Parser.Parser.IsPdf(firstFile.FilePath)) return false;

            var comicInfo = GetComicInfo(series.Format, firstFile);

            // Summary Info
            if (string.IsNullOrEmpty(comicInfo?.Summary)) return false;
            series.Summary = comicInfo.Summary;
            series.Metadata.Summary = comicInfo.Summary;

            // Get all Genres and perform matches against each genre. Then add new ones that didn't exist already
            // and Attach the Genres so they get written on save.

            // TODO: I need to do this with DB context.
            // series.Metadata.Genres = comicInfo.Genre.Split(",").Select(genre => new Genre()
            // {
            //     Name = genre.Trim(),
            //     NormalizedName = Parser.Parser.Normalize(genre)
            // });
            return true;
        }

        private ComicInfo GetComicInfo(MangaFormat format, MangaFile firstFile)
        {
            if (format is MangaFormat.Archive or MangaFormat.Epub)
            {
                return Parser.Parser.IsEpub(firstFile.FilePath) ? _bookService.GetComicInfo(firstFile.FilePath) : _archiveService.GetComicInfo(firstFile.FilePath);
            }

            return null;
        }


        /// <summary>
        /// Refreshes Metadata for a whole library
        /// </summary>
        /// <remarks>This can be heavy on memory first run</remarks>
        /// <param name="libraryId"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public async Task RefreshMetadata(int libraryId, bool forceUpdate = false)
        {
            // TODO: This will need to be split so that CoverImages is one Task, Metadata (local) is another
            var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.None);
            _logger.LogInformation("[MetadataService] Beginning metadata refresh of {LibraryName}", library.Name);

            var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
            var stopwatch = Stopwatch.StartNew();
            var totalTime = 0L;
            _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                MessageFactory.RefreshMetadataProgressEvent(library.Id, 0F));

            var i = 0;
            for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++, i++)
            {
                if (chunkInfo.TotalChunks == 0) continue;
                totalTime += stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();
                _logger.LogInformation("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                    chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);

                var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id,
                    new UserParams()
                    {
                        PageNumber = chunk,
                        PageSize = chunkInfo.ChunkSize
                    });
                _logger.LogDebug("[MetadataService] Fetched {SeriesCount} series for refresh", nonLibrarySeries.Count);

                // TODO: Pull all Metadata's here and create if they don't exist.
                // We will need them to calculate the tags for the Series itself

                var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(nonLibrarySeries.Select(s => s.Id).ToArray());


                Parallel.ForEach(nonLibrarySeries, series =>
                {
                    try
                    {
                        var chapterMetadatas = Task.Run(() => _unitOfWork.ChapterMetadataRepository.GetMetadataForChapterIds(chapterIds[series.Id]))
                            .Result;

                        _logger.LogDebug("[MetadataService] Processing series {SeriesName}", series.OriginalName);
                        var volumeUpdated = false;
                        foreach (var volume in series.Volumes)
                        {
                            var chapterUpdated = false;
                            foreach (var chapter in volume.Chapters)
                            {
                                if (!chapterMetadatas.ContainsKey(chapter.Id) || chapterMetadatas[chapter.Id].Count == 0)
                                {
                                    var metadata = DbFactory.ChapterMetadata(chapter.Id);
                                    _unitOfWork.ChapterMetadataRepository.Attach(metadata);
                                    chapterMetadatas[chapter.Id].Add(metadata);
                                }
                                chapterUpdated = UpdateMetadata(chapter, chapterMetadatas[chapter.Id][0], forceUpdate);
                            }

                            volumeUpdated = UpdateMetadata(volume, chapterUpdated || forceUpdate);
                        }

                        UpdateMetadata(series, volumeUpdated || forceUpdate);
                    }
                    catch (Exception)
                    {
                        /* Swallow exception */
                    }
                });

                if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
                {
                    _logger.LogInformation(
                        "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                        chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);

                    foreach (var series in nonLibrarySeries)
                    {
                        await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(library.Id, series.Id));
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                        chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);
                }
                var progress =  Math.Max(0F, Math.Min(1F, i * 1F / chunkInfo.TotalChunks));
                await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                    MessageFactory.RefreshMetadataProgressEvent(library.Id, progress));
            }

            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                MessageFactory.RefreshMetadataProgressEvent(library.Id, 1F));

            _logger.LogInformation("[MetadataService] Updated metadata for {SeriesNumber} series in library {LibraryName} in {ElapsedMilliseconds} milliseconds total", chunkInfo.TotalSize, library.Name, totalTime);
        }


        /// <summary>
        /// Refreshes Metadata for a Series. Will always force updates.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        public async Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = false)
        {
            var sw = Stopwatch.StartNew();
            var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
            if (series == null)
            {
                _logger.LogError("[MetadataService] Series {SeriesId} was not found on Library {LibraryId}", seriesId, libraryId);
                return;
            }
            _logger.LogInformation("[MetadataService] Beginning metadata refresh of {SeriesName}", series.Name);
            var volumeUpdated = false;
            foreach (var volume in series.Volumes)
            {
                var chapterUpdated = false;
                foreach (var chapter in volume.Chapters)
                {
                    var metadata = await _unitOfWork.ChapterMetadataRepository.GetMetadataForChapter(chapter.Id);
                    if (metadata == null)
                    {
                        metadata = DbFactory.ChapterMetadata(chapter.Id);
                        _unitOfWork.ChapterMetadataRepository.Attach(metadata);
                    }
                    chapterUpdated = UpdateMetadata(chapter, metadata, forceUpdate);
                }

                volumeUpdated = UpdateMetadata(volume, chapterUpdated || forceUpdate);
            }

            UpdateMetadata(series, volumeUpdated || forceUpdate);


            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(series.LibraryId, series.Id));
            }

            _logger.LogInformation("[MetadataService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
        }
    }
}
