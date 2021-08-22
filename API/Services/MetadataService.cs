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
        private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();
        /// <summary>
        /// Width of the Thumbnail generation
        /// </summary>
        public static readonly int ThumbnailWidth = 320; // 153w x 230h

        public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger,
            IArchiveService archiveService, IBookService bookService, IImageService imageService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _archiveService = archiveService;
            _bookService = bookService;
            _imageService = imageService;
        }

        private static bool IsCoverImageSet(byte[] coverImage, bool forceUpdate = false)
        {
            return coverImage == null || !coverImage.Any();
        }

        /// <summary>
        /// Determines whether an entity should regenerate cover image
        /// </summary>
        /// <param name="coverImage"></param>
        /// <param name="firstFile"></param>
        /// <param name="forceUpdate"></param>
        /// <param name="isCoverLocked"></param>
        /// <returns></returns>
        public static bool ShouldUpdateCoverImage(byte[] coverImage, MangaFile firstFile, bool forceUpdate = false,
            bool isCoverLocked = false)
        {
            if (isCoverLocked) return false;
            if (forceUpdate) return true;
            return (firstFile != null &&
                    new FileInfo(firstFile.FilePath).HasFileBeenModifiedSince(firstFile.LastModified) &&
                    (coverImage == null || !coverImage.Any()));
        }

        private byte[] GetCoverImage(MangaFile file, bool createThumbnail = true)
        {
            switch (file.Format)
            {
                case MangaFormat.Pdf:
                case MangaFormat.Epub:
                    return _bookService.GetCoverImage(file.FilePath, createThumbnail);
                case MangaFormat.Image:
                    var coverImage = _imageService.GetCoverFile(file);
                    return _imageService.GetCoverImage(coverImage, createThumbnail);
                case MangaFormat.Archive:
                    return _archiveService.GetCoverImage(file.FilePath, createThumbnail);
                default:
                    return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Updates the metadata for a Chapter
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public void UpdateMetadata(Chapter chapter, bool forceUpdate)
        {
            var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
           // if (chapter.CoverImageLocked) return;

            if (ShouldUpdateCoverImage(chapter.CoverImage, firstFile, forceUpdate, chapter.CoverImageLocked))
            {
                chapter.Files ??= new List<MangaFile>();
                chapter.CoverImage = GetCoverImage(firstFile);
            }

            //     if ((!chapter.CoverImageLocked
            //          && IsCoverImageSet(chapter.CoverImage, forceUpdate)
            //          && firstFile != null)
            //         || (!chapter.CoverImageLocked && (forceUpdate || new FileInfo(firstFile.FilePath).HasFileBeenModifiedSince(firstFile.LastModified))))
            // {
            //     chapter.Files ??= new List<MangaFile>();
            //     chapter.CoverImage = GetCoverImage(firstFile);
            // }
        }

        /// <summary>
        /// Updates the metadata for a Volume
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public void UpdateMetadata(Volume volume, bool forceUpdate)
        {
            if (volume == null || !IsCoverImageSet(volume.CoverImage, forceUpdate)) return;

            volume.Chapters ??= new List<Chapter>();
            var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).FirstOrDefault();

            if (firstChapter == null) return;

            volume.CoverImage = firstChapter.CoverImage;
        }

        /// <summary>
        /// Updates metadata for Series
        /// </summary>
        /// <param name="series"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public void UpdateMetadata(Series series, bool forceUpdate)
        {
            if (series == null) return;
            if (!series.CoverImageLocked && IsCoverImageSet(series.CoverImage, forceUpdate))
            {
                series.Volumes ??= new List<Volume>();
                var firstCover = series.Volumes.GetCoverImage(series.Format);
                byte[] coverImage = null;
                if (firstCover == null && series.Volumes.Any())
                {
                    // If firstCover is null and one volume, the whole series is Chapters under Vol 0.
                    if (series.Volumes.Count == 1)
                    {
                        coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparer)
                            .FirstOrDefault(c => !c.IsSpecial)?.CoverImage;
                    }

                    if (coverImage == null)
                    {
                        coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparer)
                            .FirstOrDefault()?.CoverImage;
                    }
                }
                series.CoverImage = firstCover?.CoverImage ?? coverImage;
            }

            UpdateSeriesSummary(series, forceUpdate);
        }

        private void UpdateSeriesSummary(Series series, bool forceUpdate)
        {
            if (!string.IsNullOrEmpty(series.Summary) && !forceUpdate) return;

            var isBook = series.Library.Type == LibraryType.Book;
            var firstVolume = series.Volumes.FirstWithChapters(isBook);
            var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

            var firstFile = firstChapter?.Files.FirstOrDefault();
            if (firstFile == null || (!forceUpdate && !firstFile.HasFileBeenModified())) return;
            if (Parser.Parser.IsPdf(firstFile.FilePath)) return;

            var summary = Parser.Parser.IsEpub(firstFile.FilePath) ? _bookService.GetSummaryInfo(firstFile.FilePath) : _archiveService.GetSummaryInfo(firstFile.FilePath);
            if (string.IsNullOrEmpty(series.Summary))
            {
                series.Summary = summary;
            }

            firstFile.LastModified = DateTime.Now;
        }


        /// <summary>
        /// Refreshes Metatdata for a whole library
        /// </summary>
        /// <remarks>This can be heavy on memory first run</remarks>
        /// <param name="libraryId"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public void RefreshMetadata(int libraryId, bool forceUpdate = false)
        {
            var sw = Stopwatch.StartNew();
            var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).GetAwaiter().GetResult();

            // PERF: See if we can break this up into multiple threads that process 20 series at a time then save so we can reduce amount of memory used
            _logger.LogInformation("Beginning metadata refresh of {LibraryName}", library.Name);
            foreach (var series in library.Series)
            {
                foreach (var volume in series.Volumes)
                {
                    foreach (var chapter in volume.Chapters)
                    {
                        UpdateMetadata(chapter, forceUpdate);
                    }

                    UpdateMetadata(volume, forceUpdate);
                }

                UpdateMetadata(series, forceUpdate);
                _unitOfWork.SeriesRepository.Update(series);
            }


            if (_unitOfWork.HasChanges() && Task.Run(() => _unitOfWork.CommitAsync()).Result)
            {
                _logger.LogInformation("Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);
            }
        }


        /// <summary>
        /// Refreshes Metadata for a Series. Will always force updates.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        public void RefreshMetadataForSeries(int libraryId, int seriesId)
        {
            var sw = Stopwatch.StartNew();
            var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).GetAwaiter().GetResult();

            var series = library.Series.SingleOrDefault(s => s.Id == seriesId);
            if (series == null)
            {
                _logger.LogError("Series {SeriesId} was not found on Library {LibraryName}", seriesId, libraryId);
                return;
            }
            _logger.LogInformation("Beginning metadata refresh of {SeriesName}", series.Name);
            foreach (var volume in series.Volumes)
            {
                foreach (var chapter in volume.Chapters)
                {
                    UpdateMetadata(chapter, true);
                }

                UpdateMetadata(volume, true);
            }

            UpdateMetadata(series, true);
            _unitOfWork.SeriesRepository.Update(series);


            if (_unitOfWork.HasChanges() && Task.Run(() => _unitOfWork.CommitAsync()).Result)
            {
                _logger.LogInformation("Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
            }
        }
    }
}
