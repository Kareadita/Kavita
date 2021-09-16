using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
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
        /// <summary>
        /// Width of the Thumbnail generation
        /// </summary>
        //public static readonly int ThumbnailWidth = 320; // 153w x 230h

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
        /// Determines whether an entity should regenerate cover image
        /// </summary>
        /// <param name="coverImage"></param>
        /// <param name="firstFile"></param>
        /// <param name="forceUpdate"></param>
        /// <param name="isCoverLocked"></param>
        /// <returns></returns>
        public static bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, bool forceUpdate = false,
            bool isCoverLocked = false)
        {
            if (isCoverLocked) return false;
            if (forceUpdate) return true;
            return (firstFile != null && firstFile.HasFileBeenModified()) || !HasCoverImage(coverImage);
        }


        private static bool HasCoverImage(string coverImage)
        {
            return !string.IsNullOrEmpty(coverImage) && File.Exists(coverImage);
        }

        private string GetCoverImage(MangaFile file, int volumeId, int chapterId)
        {
            // TODO: Think about a factory for naming convention & include format & hash
            file.LastModified = DateTime.Now;
            switch (file.Format)
            {
                case MangaFormat.Pdf:
                case MangaFormat.Epub:
                    return _bookService.GetCoverImage(file.FilePath, $"v{volumeId}_c{chapterId}");
                case MangaFormat.Image:
                    var coverImage = _imageService.GetCoverFile(file);
                    return _imageService.GetCoverImage(coverImage, $"v{volumeId}_c{chapterId}");
                case MangaFormat.Archive:
                    return _archiveService.GetCoverImage(file.FilePath, $"v{volumeId}_c{chapterId}");
                default:
                    return string.Empty;
            }

        }

        /// <summary>
        /// Updates the metadata for a Chapter
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public bool UpdateMetadata(Chapter chapter, bool forceUpdate)
        {
            var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();

            if (ShouldUpdateCoverImage(chapter.CoverImage, firstFile, forceUpdate, chapter.CoverImageLocked))
            {
                chapter.CoverImage = GetCoverImage(firstFile, chapter.VolumeId, chapter.Id);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the metadata for a Volume
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public bool UpdateMetadata(Volume volume, bool forceUpdate)
        {
            // We need to check if Volume coverImage matches first chapters if forceUpdate is false
            if (volume == null || !ShouldUpdateCoverImage(volume.CoverImage, null, forceUpdate
                , false)) return false;

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

            return UpdateSeriesSummary(series, forceUpdate) || madeUpdate ;
        }

        private bool UpdateSeriesSummary(Series series, bool forceUpdate)
        {
            if (!string.IsNullOrEmpty(series.Summary) && !forceUpdate) return false;

            var isBook = series.Library.Type == LibraryType.Book;
            var firstVolume = series.Volumes.FirstWithChapters(isBook);
            var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

            var firstFile = firstChapter?.Files.FirstOrDefault();
            if (firstFile == null || (!forceUpdate && !firstFile.HasFileBeenModified())) return false;
            if (Parser.Parser.IsPdf(firstFile.FilePath)) return false;

            if (series.Format is MangaFormat.Archive or MangaFormat.Epub)
            {
                var summary = Parser.Parser.IsEpub(firstFile.FilePath) ? _bookService.GetSummaryInfo(firstFile.FilePath) : _archiveService.GetSummaryInfo(firstFile.FilePath);
                if (!string.IsNullOrEmpty(series.Summary))
                {
                    series.Summary = summary;
                    firstFile.LastModified = DateTime.Now;
                    return true;
                }
            }
            firstFile.LastModified = DateTime.Now; // NOTE: Should I put this here as well since it might not have actually been parsed?
            return false;
        }


        /// <summary>
        /// Refreshes Metadata for a whole library
        /// </summary>
        /// <remarks>This can be heavy on memory first run</remarks>
        /// <param name="libraryId"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public async Task RefreshMetadata(int libraryId, bool forceUpdate = false)
        {
            var sw = Stopwatch.StartNew();
            var library = await _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId);

            // PERF: See if we can break this up into multiple threads that process 20 series at a time then save so we can reduce amount of memory used
            _logger.LogInformation("Beginning metadata refresh of {LibraryName}", library.Name);
            foreach (var series in library.Series)
            {
                var volumeUpdated = false;
                foreach (var volume in series.Volumes)
                {
                    var chapterUpdated = false;
                    foreach (var chapter in volume.Chapters)
                    {
                        chapterUpdated = UpdateMetadata(chapter, forceUpdate);
                    }

                    volumeUpdated = UpdateMetadata(volume, chapterUpdated || forceUpdate);
                }

                UpdateMetadata(series, volumeUpdated || forceUpdate);
                _unitOfWork.SeriesRepository.Update(series);
            }


            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                _logger.LogInformation("Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);
            }
        }


        /// <summary>
        /// Refreshes Metadata for a Series. Will always force updates.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        public async Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = false)
        {
            var sw = Stopwatch.StartNew();
            var library = await _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId);

            var series = library.Series.SingleOrDefault(s => s.Id == seriesId);
            if (series == null)
            {
                _logger.LogError("Series {SeriesId} was not found on Library {LibraryName}", seriesId, libraryId);
                return;
            }
            _logger.LogInformation("Beginning metadata refresh of {SeriesName}", series.Name);
            var volumeUpdated = false;
            foreach (var volume in series.Volumes)
            {
                var chapterUpdated = false;
                foreach (var chapter in volume.Chapters)
                {
                    chapterUpdated = UpdateMetadata(chapter, forceUpdate);
                }

                volumeUpdated = UpdateMetadata(volume, chapterUpdated || forceUpdate);
            }

            UpdateMetadata(series, volumeUpdated || forceUpdate);
            _unitOfWork.SeriesRepository.Update(series);


            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                _logger.LogInformation("Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
                await _messageHub.Clients.All.SendAsync(SignalREvents.ScanSeries, MessageFactory.RefreshMetadataEvent(libraryId, seriesId));
            }
        }
    }
}
