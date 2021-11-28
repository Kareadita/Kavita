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
using API.Entities.Interfaces;
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
        public static bool ShouldUpdateCoverImage(string coverImage, MangaFile firstFile, DateTime chapterCreated, bool forceUpdate = false,
            bool isCoverLocked = false, string coverImageDirectory = null)
        {
            if (string.IsNullOrEmpty(coverImageDirectory))
            {
                coverImageDirectory = DirectoryService.CoverImageDirectory;
            }

            var fileExists = File.Exists(Path.Join(coverImageDirectory, coverImage));
            if (isCoverLocked && fileExists) return false;
            if (forceUpdate) return true;
            return (firstFile != null && (firstFile.HasFileBeenModifiedSince(chapterCreated) || firstFile.HasFileBeenModified())) || !HasCoverImage(coverImage, fileExists);
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
                case MangaFormat.Unknown:
                default:
                    return string.Empty;
            }

        }

        /// <summary>
        /// Updates the metadata for a Chapter
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        private bool UpdateChapterCoverImage(Chapter chapter, bool forceUpdate)
        {
            var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();

            if (!ShouldUpdateCoverImage(chapter.CoverImage, firstFile, chapter.Created, forceUpdate, chapter.CoverImageLocked))
                return false;

            _logger.LogDebug("[MetadataService] Generating cover image for {File}", firstFile?.FilePath);
            chapter.CoverImage = GetCoverImage(firstFile, chapter.VolumeId, chapter.Id);

            return true;
        }

        private void UpdateChapterMetadata(Chapter chapter, ICollection<Person> allPeople, bool forceUpdate)
        {
            var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
            if (firstFile == null || HasFileNotChangedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

            if (Parser.Parser.IsArchive(firstFile.FilePath))
            {
                UpdateChapterFromComicInfo(chapter.ChapterMetadata, allPeople, firstFile);
            }
            else
            {
                // TODO: Update from epub
            }
        }

        private static bool HasFileNotChangedSinceCreationOrLastScan(IEntityDate chapter, bool forceUpdate, MangaFile? firstFile)
        {
            return firstFile == null || (!forceUpdate && !(!firstFile.HasFileBeenModifiedSince(chapter.Created) || firstFile.HasFileBeenModified()));
        }

        private void UpdateChapterFromComicInfo(ChapterMetadata chapterMetadata, ICollection<Person> allPeople, MangaFile firstFile)
        {
            var comicInfo = _archiveService.GetComicInfo(firstFile.FilePath);
            if (comicInfo == null) return;

            if (!string.IsNullOrEmpty(comicInfo.Title))
            {
                chapterMetadata.Title = comicInfo.Title;
            }

            if (!string.IsNullOrEmpty(comicInfo.Colorist))
            {
                UpdatePeople(allPeople, comicInfo.Colorist.Split(","), PersonRole.Colorist,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Writer))
            {
                UpdatePeople(allPeople, comicInfo.Writer.Split(","), PersonRole.Writer,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Editor))
            {
                UpdatePeople(allPeople, comicInfo.Editor.Split(","), PersonRole.Editor,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Inker))
            {
                UpdatePeople(allPeople, comicInfo.Inker.Split(","), PersonRole.Inker,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Letterer))
            {
                UpdatePeople(allPeople, comicInfo.Letterer.Split(","), PersonRole.Letterer,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Penciller))
            {
                UpdatePeople(allPeople, comicInfo.Penciller.Split(","), PersonRole.Penciller,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.CoverArtist))
            {
                UpdatePeople(allPeople, comicInfo.CoverArtist.Split(","), PersonRole.CoverArtist,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }

            if (!string.IsNullOrEmpty(comicInfo.Publisher))
            {
                UpdatePeople(allPeople, comicInfo.Publisher.Split(","), PersonRole.Publisher,
                    person => AddPersonIfNotOnMetadata(chapterMetadata.People, person));
            }
        }

        private static void UpdatePeople(ICollection<Person> allPeople, IEnumerable<string> names, PersonRole role, Action<Person> action)
        {
            var allPeopleTypeRole = allPeople.Where(p => p.Role == role).ToList();

            foreach (var name in names)
            {
                var normalizedName = Parser.Parser.Normalize(name);
                var person = allPeopleTypeRole.FirstOrDefault(p =>
                    p.NormalizedName.Equals(normalizedName));
                if (person == null)
                {
                    person = new Person()
                    {
                        Name = name,
                        NormalizedName = normalizedName,
                        Role = role
                    };
                    allPeople.Add(person);
                }



                action(person);
            }
        }

        /// <summary>
        /// Updates the cover image for a Volume
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        private bool UpdateVolumeCoverImage(Volume volume, bool forceUpdate)
        {
            // We need to check if Volume coverImage matches first chapters if forceUpdate is false
            if (volume == null || !ShouldUpdateCoverImage(volume.CoverImage, null, volume.Created, forceUpdate)) return false;

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
        private async Task UpdateSeriesCoverImage(Series series, bool forceUpdate)
        {
            if (series == null) return;

            // NOTE: This will fail if we replace the cover of the first volume on a first scan. Because the series will already have a cover image
            if (!ShouldUpdateCoverImage(series.CoverImage, null, series.Created, forceUpdate, series.CoverImageLocked))
                return;

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
                }

                if (!HasCoverImage(coverImage))
                {
                    coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                        .FirstOrDefault()?.CoverImage;
                }
            }
            series.CoverImage = firstCover?.CoverImage ?? coverImage;
            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(series.LibraryId, series.Id));
        }

        private void UpdateSeriesMetadata(Series series, ICollection<Person> allPeople, bool forceUpdate)
        {
            // NOTE: This can be problematic when the file changes and a summary already exists, but it is likely
            // better to let the user kick off a refresh metadata on an individual Series than having overhead of
            // checking File last write time.

            // If a series is just a special, then the original code of
            // var firstVolume = series.Volumes.FirstWithChapters(isBook);
            // will fail.


            if (!string.IsNullOrEmpty(series.Metadata.Summary) && !forceUpdate) return;

            var isBook = series.Library.Type == LibraryType.Book;
            var firstVolume = series.Volumes.OrderBy(c => c.Number, new ChapterSortComparer()).FirstWithChapters(isBook);
            var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

            var firstFile = firstChapter?.Files.FirstOrDefault();
            if (firstFile == null || HasFileNotChangedSinceCreationOrLastScan(firstChapter, forceUpdate, firstFile)) return;
            if (Parser.Parser.IsPdf(firstFile.FilePath)) return;

            var comicInfo = GetComicInfo(series.Format, firstFile);
            if (comicInfo == null) return;

            // BUG: At a series level, I need all the People to be saved already so I can properly match



            // Summary Info
            if (!string.IsNullOrEmpty(comicInfo.Summary))
            {
                series.Metadata.Summary = comicInfo.Summary;
            }

            foreach (var chapter in series.Volumes.SelectMany(volume => volume.Chapters))
            {
                UpdatePeople(allPeople, chapter.ChapterMetadata.People.Where(p => p.Role == PersonRole.Writer).Select(p => p.Name), PersonRole.Writer,
                    person => AddPersonIfNotOnMetadata(series.Metadata.People, person));

                UpdatePeople(allPeople, chapter.ChapterMetadata.People.Where(p => p.Role == PersonRole.CoverArtist).Select(p => p.Name), PersonRole.CoverArtist,
                    person => AddPersonIfNotOnMetadata(series.Metadata.People, person));

                UpdatePeople(allPeople, chapter.ChapterMetadata.People.Where(p => p.Role == PersonRole.Publisher).Select(p => p.Name), PersonRole.Publisher,
                    person => AddPersonIfNotOnMetadata(series.Metadata.People, person));
            }
        }

        private static void AddPersonIfNotOnMetadata(ICollection<Person> metadataPeople, Person person)
        {
            var existingPerson = metadataPeople.SingleOrDefault(p =>
                p.NormalizedName == Parser.Parser.Normalize(person.Name) && p.Role == person.Role);
            if (existingPerson == null)
            {
                metadataPeople.Add(person);
            }

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
        ///
        /// </summary>
        /// <remarks>This cannot have any Async code within. It is used within Parallel.ForEach</remarks>
        /// <param name="series"></param>
        /// <param name="forceUpdate"></param>
        private async Task ProcessSeriesMetadataUpdate(Series series, IDictionary<int, IList<int>> chapterIds, IList<Person> allPeople, bool forceUpdate)
        {
            _logger.LogDebug("[MetadataService] Processing series {SeriesName}", series.OriginalName);
            try
            {
                var volumeUpdated = false;
                foreach (var volume in series.Volumes)
                {
                    var chapterUpdated = false;
                    foreach (var chapter in volume.Chapters)
                    {
                        //GetChapterMetadataOrCreate(chapterMetadatas, chapter);
                        if (chapter.ChapterMetadata == null)
                        {
                            var metadata = DbFactory.ChapterMetadata(chapter.Id);
                            _unitOfWork.ChapterMetadataRepository.Attach(metadata);
                            chapter.ChapterMetadata ??= metadata;
                        }


                        chapterUpdated = UpdateChapterCoverImage(chapter, forceUpdate);
                        UpdateChapterMetadata(chapter, allPeople, forceUpdate);
                    }

                    volumeUpdated = UpdateVolumeCoverImage(volume, chapterUpdated || forceUpdate);
                }

                await UpdateSeriesCoverImage(series, volumeUpdated || forceUpdate);
                UpdateSeriesMetadata(series, allPeople, forceUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MetadataService] There was an exception during updating metadata for {SeriesName} ", series.Name);
            }

            // TODO: Remove any People/Genre entries that no longer exist
        }

        /// <summary>
        /// Attempts to get an existing metadata entity else creates a new one and attatches to the chapter
        /// </summary>
        /// <param name="chapterMetadatas"></param>
        /// <param name="chapter"></param>
        private void GetChapterMetadataOrCreate(IDictionary<int, IList<ChapterMetadata>> chapterMetadatas, Chapter chapter)
        {
            ChapterMetadata metadata;
            if (!chapterMetadatas.ContainsKey(chapter.Id) || chapterMetadatas[chapter.Id].Count == 0)
            {
                metadata = DbFactory.ChapterMetadata(chapter.Id);
                _unitOfWork.ChapterMetadataRepository.Attach(metadata);
                chapterMetadatas[chapter.Id].Add(metadata); // This is causing the loop to break
            }
            else
            {
                metadata = chapterMetadatas[chapter.Id][0];
            }

            chapter.ChapterMetadata ??= metadata;
        }


        /// <summary>
        /// Refreshes Metadata for a whole library
        /// </summary>
        /// <remarks>This can be heavy on memory first run</remarks>
        /// <param name="libraryId"></param>
        /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
        public async Task RefreshMetadata(int libraryId, bool forceUpdate = false)
        {
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

                var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(nonLibrarySeries.Select(s => s.Id).ToArray());
                var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();


                var seriesIndex = 0;
                foreach (var series in nonLibrarySeries)
                {
                    try
                    {
                        await ProcessSeriesMetadataUpdate(series, chapterIds, allPeople, forceUpdate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MetadataService] There was an exception during metadata refresh for {SeriesName}", series.Name);
                    }
                    var index = chunk * seriesIndex;
                    var progress =  Math.Max(0F, Math.Min(1F, index * 1F / chunkInfo.TotalSize));
                    await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                        MessageFactory.RefreshMetadataProgressEvent(library.Id, progress));
                    seriesIndex++;
                }



                await _unitOfWork.CommitAsync();
                foreach (var series in nonLibrarySeries)
                {
                    await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(library.Id, series.Id));
                }
                _logger.LogInformation(
                    "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                    chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);


                // var progress =  Math.Max(0F, Math.Min(1F, i * 1F / chunkInfo.TotalChunks));
                // await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                //     MessageFactory.RefreshMetadataProgressEvent(library.Id, progress));
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

            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                MessageFactory.RefreshMetadataProgressEvent(libraryId, 0F));

            var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(new [] { seriesId });
            var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
            await ProcessSeriesMetadataUpdate(series, chapterIds, allPeople, forceUpdate);

            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
                MessageFactory.RefreshMetadataProgressEvent(libraryId, 1F));


            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(series.LibraryId, series.Id));
            }

            _logger.LogInformation("[MetadataService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
        }
    }
}
