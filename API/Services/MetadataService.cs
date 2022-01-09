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
using API.Data.Scanner;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IMetadataService
{
    /// <summary>
    /// Recalculates metadata for all entities in a library.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate"></param>
    Task RefreshMetadata(int libraryId, bool forceUpdate = false);
    /// <summary>
    /// Performs a forced refresh of metadata just for a series and it's nested entities
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = false);
}

public class MetadataService : IMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MetadataService> _logger;
    private readonly IHubContext<MessageHub> _messageHub;
    private readonly ICacheHelper _cacheHelper;
    private readonly IReadingItemService _readingItemService;
    private readonly IDirectoryService _directoryService;
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();
    public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger,
        IHubContext<MessageHub> messageHub, ICacheHelper cacheHelper,
        IReadingItemService readingItemService, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _messageHub = messageHub;
        _cacheHelper = cacheHelper;
        _readingItemService = readingItemService;
        _directoryService = directoryService;
    }

    /// <summary>
    /// Updates the metadata for a Chapter
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private bool UpdateChapterCoverImage(Chapter chapter, bool forceUpdate)
    {
        var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();

        if (!_cacheHelper.ShouldUpdateCoverImage(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, chapter.CoverImage), firstFile, chapter.Created, forceUpdate, chapter.CoverImageLocked))
            return false;

        if (firstFile == null) return false;

        _logger.LogDebug("[MetadataService] Generating cover image for {File}", firstFile?.FilePath);
        chapter.CoverImage = _readingItemService.GetCoverImage(firstFile.FilePath, ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId), firstFile.Format);

        return true;
    }

    private void UpdateChapterMetadata(Chapter chapter, ICollection<Person> allPeople, ICollection<Tag> allTags, ICollection<Genre> allGenres, bool forceUpdate)
    {
        var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
        if (firstFile == null || _cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, forceUpdate, firstFile)) return;

        UpdateChapterFromComicInfo(chapter, allPeople, allTags, allGenres, firstFile);
        firstFile.UpdateLastModified();
    }

    private void UpdateChapterFromComicInfo(Chapter chapter, ICollection<Person> allPeople, ICollection<Tag> allTags, ICollection<Genre> allGenres, MangaFile firstFile)
    {
        var comicInfo = _readingItemService.GetComicInfo(firstFile.FilePath, firstFile.Format);
        if (comicInfo == null) return;

        chapter.AgeRating = ComicInfo.ConvertAgeRatingToEnum(comicInfo.AgeRating);

        if (!string.IsNullOrEmpty(comicInfo.Title))
        {
            chapter.TitleName = comicInfo.Title.Trim();
        }

        if (!string.IsNullOrEmpty(comicInfo.Summary))
        {
            chapter.Summary = comicInfo.Summary;
        }

        if (!string.IsNullOrEmpty(comicInfo.LanguageISO))
        {
            chapter.Language = comicInfo.LanguageISO;
        }

        if (comicInfo.Count > 0)
        {
            chapter.TotalCount = comicInfo.Count;
        }

        if (int.Parse(comicInfo.Number) > 0)
        {
            chapter.Count = int.Parse(comicInfo.Number);
        }




        if (comicInfo.Year > 0)
        {
            var day = Math.Max(comicInfo.Day, 1);
            var month = Math.Max(comicInfo.Month, 1);
            chapter.ReleaseDate = DateTime.Parse($"{month}/{day}/{comicInfo.Year}");
        }

        if (!string.IsNullOrEmpty(comicInfo.Colorist))
        {
            var people = comicInfo.Colorist.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Colorist);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Colorist,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Characters))
        {
            var people = comicInfo.Characters.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Character);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Character,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Translator))
        {
            var people = comicInfo.Translator.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Translator);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Translator,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Tags))
        {
            var tags = comicInfo.Tags.Split(",").Select(s => s.Trim()).ToList();
            // Remove all tags that aren't matching between chapter tags and metadata
            TagHelper.KeepOnlySameTagBetweenLists(chapter.Tags, tags.Select(t => DbFactory.Tag(t, false)).ToList());
            TagHelper.UpdateTag(allTags, tags, false,
                (tag, added) =>
                {
                    chapter.Tags.Add(tag);
                });
        }

        if (!string.IsNullOrEmpty(comicInfo.Writer))
        {
            var people = comicInfo.Writer.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Writer);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Writer,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Editor))
        {
            var people = comicInfo.Editor.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Editor);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Editor,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Inker))
        {
            var people = comicInfo.Inker.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Inker);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Inker,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Letterer))
        {
            var people = comicInfo.Letterer.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Letterer);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Letterer,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Penciller))
        {
            var people = comicInfo.Penciller.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Penciller);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Penciller,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.CoverArtist))
        {
            var people = comicInfo.CoverArtist.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.CoverArtist);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.CoverArtist,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Publisher))
        {
            var people = comicInfo.Publisher.Split(",");
            PersonHelper.RemovePeople(chapter.People, people, PersonRole.Publisher);
            PersonHelper.UpdatePeople(allPeople, people, PersonRole.Publisher,
                person => PersonHelper.AddPersonIfNotExists(chapter.People, person));
        }

        if (!string.IsNullOrEmpty(comicInfo.Genre))
        {
            var genres = comicInfo.Genre.Split(",");
            GenreHelper.KeepOnlySameGenreBetweenLists(chapter.Genres, genres.Select(g => DbFactory.Genre(g, false)).ToList());
            GenreHelper.UpdateGenre(allGenres, genres, false,
                genre => chapter.Genres.Add(genre));
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
        if (volume == null || !_cacheHelper.ShouldUpdateCoverImage(
                _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, volume.CoverImage),
                null, volume.Created, forceUpdate)) return false;

        volume.Chapters ??= new List<Chapter>();
        var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).FirstOrDefault();
        if (firstChapter == null) return false;

        volume.CoverImage = firstChapter.CoverImage;
        return true;
    }

    /// <summary>
    /// Updates cover image for Series
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    private void UpdateSeriesCoverImage(Series series, bool forceUpdate)
    {
        if (series == null) return;

        if (!_cacheHelper.ShouldUpdateCoverImage(_directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, series.CoverImage),
                null, series.Created, forceUpdate, series.CoverImageLocked))
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

            if (!_cacheHelper.CoverImageExists(coverImage))
            {
                coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparerForInChapterSorting)
                    .FirstOrDefault()?.CoverImage;
            }
        }
        series.CoverImage = firstCover?.CoverImage ?? coverImage;
    }

    private static void UpdateSeriesMetadata(Series series, ICollection<Person> allPeople, ICollection<Genre> allGenres, ICollection<Tag> allTags, bool forceUpdate)
    {
        var isBook = series.Library.Type == LibraryType.Book;
        var firstVolume = series.Volumes.OrderBy(c => c.Number, new ChapterSortComparer()).FirstWithChapters(isBook);
        var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

        var firstFile = firstChapter?.Files.FirstOrDefault();
        if (firstFile == null) return;
        if (Parser.Parser.IsPdf(firstFile.FilePath)) return;

        var chapters = series.Volumes.SelectMany(volume => volume.Chapters).ToList();

        // Update Metadata based on Chapter metadata
        series.Metadata.ReleaseYear = chapters.Min(c => c.ReleaseDate.Year);

        if (series.Metadata.ReleaseYear < 1000)
        {
            // Not a valid year, default to 0
            series.Metadata.ReleaseYear = 0;
        }

        // Set the AgeRating as highest in all the comicInfos
        series.Metadata.AgeRating = chapters.Max(chapter => chapter.AgeRating);


        series.Metadata.Count = chapters.Max(chapter => chapter.TotalCount);
        series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
        if (chapters.Max(chapter => chapter.Count) >= series.Metadata.Count && series.Metadata.Count > 0)
        {
            series.Metadata.PublicationStatus = PublicationStatus.Completed;
        }

        if (!string.IsNullOrEmpty(firstChapter.Summary))
        {
            series.Metadata.Summary = firstChapter.Summary;
        }

        if (!string.IsNullOrEmpty(firstChapter.Language))
        {
            series.Metadata.Language = firstChapter.Language;
        }


        // Handle People
        foreach (var chapter in chapters)
        {
            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Writer).Select(p => p.Name), PersonRole.Writer,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.CoverArtist).Select(p => p.Name), PersonRole.CoverArtist,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Publisher).Select(p => p.Name), PersonRole.Publisher,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Character).Select(p => p.Name), PersonRole.Character,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Colorist).Select(p => p.Name), PersonRole.Colorist,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Editor).Select(p => p.Name), PersonRole.Editor,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Inker).Select(p => p.Name), PersonRole.Inker,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Letterer).Select(p => p.Name), PersonRole.Letterer,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Penciller).Select(p => p.Name), PersonRole.Penciller,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            PersonHelper.UpdatePeople(allPeople, chapter.People.Where(p => p.Role == PersonRole.Translator).Select(p => p.Name), PersonRole.Translator,
                person => PersonHelper.AddPersonIfNotExists(series.Metadata.People, person));

            TagHelper.UpdateTag(allTags, chapter.Tags.Select(t => t.Title), false, (tag, added) =>
                TagHelper.AddTagIfNotExists(series.Metadata.Tags, tag));

            GenreHelper.UpdateGenre(allGenres, chapter.Genres.Select(t => t.Title), false, genre =>
                GenreHelper.AddGenreIfNotExists(series.Metadata.Genres, genre));
        }

        var people = chapters.SelectMany(c => c.People).ToList();
        PersonHelper.KeepOnlySamePeopleBetweenLists(series.Metadata.People,
            people, person => series.Metadata.People.Remove(person));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="series"></param>
    /// <param name="forceUpdate"></param>
    private void ProcessSeriesMetadataUpdate(Series series, ICollection<Person> allPeople, ICollection<Genre> allGenres, ICollection<Tag> allTags, bool forceUpdate)
    {
        _logger.LogDebug("[MetadataService] Processing series {SeriesName}", series.OriginalName);
        try
        {
            var volumeIndex = 0;
            var firstVolumeUpdated = false;
            foreach (var volume in series.Volumes)
            {
                var firstChapterUpdated = false; // This only needs to be FirstChapter updated
                var index = 0;
                foreach (var chapter in volume.Chapters)
                {
                    var chapterUpdated = UpdateChapterCoverImage(chapter, forceUpdate);
                    // If cover was update, either the file has changed or first scan and we should force a metadata update
                    UpdateChapterMetadata(chapter, allPeople, allTags, allGenres, forceUpdate || chapterUpdated);
                    if (index == 0 && chapterUpdated)
                    {
                        firstChapterUpdated = true;
                    }

                    index++;
                }

                var volumeUpdated = UpdateVolumeCoverImage(volume, firstChapterUpdated || forceUpdate);
                if (volumeIndex == 0 && volumeUpdated)
                {
                    firstVolumeUpdated = true;
                }
                volumeIndex++;
            }

            UpdateSeriesCoverImage(series, firstVolumeUpdated || forceUpdate);
            UpdateSeriesMetadata(series, allPeople, allGenres, allTags, forceUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MetadataService] There was an exception during updating metadata for {SeriesName} ", series.Name);
        }
    }


    /// <summary>
    /// Refreshes Metadata for a whole library
    /// </summary>
    /// <remarks>This can be heavy on memory first run</remarks>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate">Force updating cover image even if underlying file has not been modified or chapter already has a cover image</param>
    public async Task RefreshMetadata(int libraryId, bool forceUpdate = false)
    {
        // TODO: Think about splitting the comicinfo stuff into a separate task
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.None);
        _logger.LogInformation("[MetadataService] Beginning metadata refresh of {LibraryName}", library.Name);

        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;
        _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
        await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
            MessageFactory.RefreshMetadataProgressEvent(library.Id, 0F));

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
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

            var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
            var allGenres = await _unitOfWork.GenreRepository.GetAllGenresAsync();
            var allTags = await _unitOfWork.TagRepository.GetAllTagsAsync();


            var seriesIndex = 0;
            foreach (var series in nonLibrarySeries)
            {
                try
                {
                    ProcessSeriesMetadataUpdate(series, allPeople, allGenres, allTags, forceUpdate);
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
        }

        await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
            MessageFactory.RefreshMetadataProgressEvent(library.Id, 1F));

        await RemoveAbandonedMetadataKeys();


        _logger.LogInformation("[MetadataService] Updated metadata for {SeriesNumber} series in library {LibraryName} in {ElapsedMilliseconds} milliseconds total", chunkInfo.TotalSize, library.Name, totalTime);
    }

    private async Task RemoveAbandonedMetadataKeys()
    {
        await _unitOfWork.TagRepository.RemoveAllTagNoLongerAssociated();
        await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
        await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
    }

    // TODO: I can probably refactor RefreshMetadata and RefreshMetadataForSeries to be the same by utilizing chunk size of 1, so most of the code can be the same.
    private async Task PerformScan(Library library, bool forceUpdate, Action<int, Chunk> action)
    {
        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;
        _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);
        await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
            MessageFactory.RefreshMetadataProgressEvent(library.Id, 0F));

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            totalTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            action(chunk, chunkInfo);

            // _logger.LogInformation("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
            //     chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);
            // var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id,
            //     new UserParams()
            //     {
            //         PageNumber = chunk,
            //         PageSize = chunkInfo.ChunkSize
            //     });
            // _logger.LogDebug("[MetadataService] Fetched {SeriesCount} series for refresh", nonLibrarySeries.Count);
            //
            // var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(nonLibrarySeries.Select(s => s.Id).ToArray());
            // var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
            // var allGenres = await _unitOfWork.GenreRepository.GetAllGenres();
            //
            //
            // var seriesIndex = 0;
            // foreach (var series in nonLibrarySeries)
            // {
            //     try
            //     {
            //         ProcessSeriesMetadataUpdate(series, chapterIds, allPeople, allGenres, forceUpdate);
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogError(ex, "[MetadataService] There was an exception during metadata refresh for {SeriesName}", series.Name);
            //     }
            //     var index = chunk * seriesIndex;
            //     var progress =  Math.Max(0F, Math.Min(1F, index * 1F / chunkInfo.TotalSize));
            //
            //     await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
            //         MessageFactory.RefreshMetadataProgressEvent(library.Id, progress));
            //     seriesIndex++;
            // }

            await _unitOfWork.CommitAsync();
        }
    }



    /// <summary>
    /// Refreshes Metadata for a Series. Will always force updates.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    public async Task RefreshMetadataForSeries(int libraryId, int seriesId, bool forceUpdate = true)
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

        var allPeople = await _unitOfWork.PersonRepository.GetAllPeople();
        var allGenres = await _unitOfWork.GenreRepository.GetAllGenresAsync();
        var allTags = await _unitOfWork.TagRepository.GetAllTagsAsync();

        ProcessSeriesMetadataUpdate(series, allPeople, allGenres, allTags, forceUpdate);

        await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadataProgress,
            MessageFactory.RefreshMetadataProgressEvent(libraryId, 1F));


        if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
        {
            await _messageHub.Clients.All.SendAsync(SignalREvents.RefreshMetadata, MessageFactory.RefreshMetadataEvent(series.LibraryId, series.Id));
        }

        await RemoveAbandonedMetadataKeys();

        _logger.LogInformation("[MetadataService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
    }
}
