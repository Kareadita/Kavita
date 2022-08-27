using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services;


public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto);
    Task<bool> UpdateRating(AppUser user, UpdateSeriesRatingDto updateSeriesRatingDto);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds);

}

public class SeriesService : ISeriesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<SeriesService> _logger;

    public SeriesService(IUnitOfWork unitOfWork, IEventHub eventHub, ITaskScheduler taskScheduler, ILogger<SeriesService> logger)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _taskScheduler = taskScheduler;
        _logger = logger;
    }

    /// <summary>
    /// Returns the first chapter for a series to extract metadata from (ie Summary, etc)
    /// </summary>
    /// <param name="series"></param>
    /// <param name="isBookLibrary"></param>
    /// <returns></returns>
    public static Chapter GetFirstChapterForMetadata(Series series, bool isBookLibrary)
    {
        return series.Volumes.OrderBy(v => v.Number, ChapterSortComparer.Default)
            .SelectMany(v => v.Chapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default))
            .FirstOrDefault();
    }

    public async Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
    {
        try
        {
            var seriesId = updateSeriesMetadataDto.SeriesMetadata.SeriesId;
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            var allCollectionTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).ToList();
            var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToList();
            var allPeople = (await _unitOfWork.PersonRepository.GetAllPeople()).ToList();
            var allTags = (await _unitOfWork.TagRepository.GetAllTagsAsync()).ToList();

            series.Metadata ??= DbFactory.SeriesMetadata(updateSeriesMetadataDto.CollectionTags
                .Select(dto => DbFactory.CollectionTag(dto.Id, dto.Title, dto.Summary, dto.Promoted)).ToList());

            if (series.Metadata.AgeRating != updateSeriesMetadataDto.SeriesMetadata.AgeRating)
            {
                series.Metadata.AgeRating = updateSeriesMetadataDto.SeriesMetadata.AgeRating;
                series.Metadata.AgeRatingLocked = true;
            }

            if (series.Metadata.PublicationStatus != updateSeriesMetadataDto.SeriesMetadata.PublicationStatus)
            {
                series.Metadata.PublicationStatus = updateSeriesMetadataDto.SeriesMetadata.PublicationStatus;
                series.Metadata.PublicationStatusLocked = true;
            }

            // This shouldn't be needed post v0.5.3 release
            if (string.IsNullOrEmpty(series.Metadata.Summary))
            {
                series.Metadata.Summary = string.Empty;
            }

            if (string.IsNullOrEmpty(updateSeriesMetadataDto.SeriesMetadata.Summary))
            {
                updateSeriesMetadataDto.SeriesMetadata.Summary = string.Empty;
            }

            if (series.Metadata.Summary != updateSeriesMetadataDto.SeriesMetadata.Summary.Trim())
            {
                series.Metadata.Summary = updateSeriesMetadataDto.SeriesMetadata?.Summary.Trim();
                series.Metadata.SummaryLocked = true;
            }

            if (series.Metadata.Language != updateSeriesMetadataDto.SeriesMetadata?.Language)
            {
                series.Metadata.Language = updateSeriesMetadataDto.SeriesMetadata?.Language;
                series.Metadata.LanguageLocked = true;
            }

            series.Metadata.CollectionTags ??= new List<CollectionTag>();
            UpdateRelatedList(updateSeriesMetadataDto.CollectionTags, series, allCollectionTags, (tag) =>
            {
                series.Metadata.CollectionTags.Add(tag);
            });

            series.Metadata.Genres ??= new List<Genre>();
            UpdateGenreList(updateSeriesMetadataDto.SeriesMetadata?.Genres, series, allGenres, (genre) =>
            {
                series.Metadata.Genres.Add(genre);
            }, () => series.Metadata.GenresLocked = true);

            series.Metadata.Tags ??= new List<Tag>();
            UpdateTagList(updateSeriesMetadataDto.SeriesMetadata.Tags, series, allTags, (tag) =>
            {
                series.Metadata.Tags.Add(tag);
            }, () => series.Metadata.TagsLocked = true);

            void HandleAddPerson(Person person)
            {
                PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                allPeople.Add(person);
            }

            series.Metadata.People ??= new List<Person>();
            UpdatePeopleList(PersonRole.Writer, updateSeriesMetadataDto.SeriesMetadata.Writers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.WriterLocked = true);
            UpdatePeopleList(PersonRole.Character, updateSeriesMetadataDto.SeriesMetadata.Characters, series, allPeople,
                HandleAddPerson,  () => series.Metadata.CharacterLocked = true);
            UpdatePeopleList(PersonRole.Colorist, updateSeriesMetadataDto.SeriesMetadata.Colorists, series, allPeople,
                HandleAddPerson,  () => series.Metadata.ColoristLocked = true);
            UpdatePeopleList(PersonRole.Editor, updateSeriesMetadataDto.SeriesMetadata.Editors, series, allPeople,
                HandleAddPerson,  () => series.Metadata.EditorLocked = true);
            UpdatePeopleList(PersonRole.Inker, updateSeriesMetadataDto.SeriesMetadata.Inkers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.InkerLocked = true);
            UpdatePeopleList(PersonRole.Letterer, updateSeriesMetadataDto.SeriesMetadata.Letterers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.LettererLocked = true);
            UpdatePeopleList(PersonRole.Penciller, updateSeriesMetadataDto.SeriesMetadata.Pencillers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.PencillerLocked = true);
            UpdatePeopleList(PersonRole.Publisher, updateSeriesMetadataDto.SeriesMetadata.Publishers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.PublisherLocked = true);
            UpdatePeopleList(PersonRole.Translator, updateSeriesMetadataDto.SeriesMetadata.Translators, series, allPeople,
                HandleAddPerson,  () => series.Metadata.TranslatorLocked = true);
            UpdatePeopleList(PersonRole.CoverArtist, updateSeriesMetadataDto.SeriesMetadata.CoverArtists, series, allPeople,
                HandleAddPerson,  () => series.Metadata.CoverArtistLocked = true);

            series.Metadata.AgeRatingLocked = updateSeriesMetadataDto.SeriesMetadata.AgeRatingLocked;
            series.Metadata.PublicationStatusLocked = updateSeriesMetadataDto.SeriesMetadata.PublicationStatusLocked;
            series.Metadata.LanguageLocked = updateSeriesMetadataDto.SeriesMetadata.LanguageLocked;
            series.Metadata.GenresLocked = updateSeriesMetadataDto.SeriesMetadata.GenresLocked;
            series.Metadata.TagsLocked = updateSeriesMetadataDto.SeriesMetadata.TagsLocked;
            series.Metadata.CharacterLocked = updateSeriesMetadataDto.SeriesMetadata.CharactersLocked;
            series.Metadata.ColoristLocked = updateSeriesMetadataDto.SeriesMetadata.ColoristsLocked;
            series.Metadata.EditorLocked = updateSeriesMetadataDto.SeriesMetadata.EditorsLocked;
            series.Metadata.InkerLocked = updateSeriesMetadataDto.SeriesMetadata.InkersLocked;
            series.Metadata.LettererLocked = updateSeriesMetadataDto.SeriesMetadata.LetterersLocked;
            series.Metadata.PencillerLocked = updateSeriesMetadataDto.SeriesMetadata.PencillersLocked;
            series.Metadata.PublisherLocked = updateSeriesMetadataDto.SeriesMetadata.PublishersLocked;
            series.Metadata.TranslatorLocked = updateSeriesMetadataDto.SeriesMetadata.TranslatorsLocked;
            series.Metadata.CoverArtistLocked = updateSeriesMetadataDto.SeriesMetadata.CoverArtistsLocked;
            series.Metadata.WriterLocked = updateSeriesMetadataDto.SeriesMetadata.WritersLocked;
            series.Metadata.SummaryLocked = updateSeriesMetadataDto.SeriesMetadata.SummaryLocked;

            if (!_unitOfWork.HasChanges())
            {
                return true;
            }

            if (await _unitOfWork.CommitAsync())
            {
                foreach (var tag in updateSeriesMetadataDto.CollectionTags)
                {
                    await _eventHub.SendMessageAsync(MessageFactory.SeriesAddedToCollection,
                        MessageFactory.SeriesAddedToCollectionEvent(tag.Id,
                            updateSeriesMetadataDto.SeriesMetadata.SeriesId), false);
                }

                await _eventHub.SendMessageAsync(MessageFactory.ScanSeries,
                    MessageFactory.ScanSeriesEvent(series.LibraryId, series.Id, series.Name), false);

                await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating metadata");
            await _unitOfWork.RollbackAsync();
        }

        return false;
    }


    private static void UpdateRelatedList(ICollection<CollectionTagDto> tags, Series series, IReadOnlyCollection<CollectionTag> allTags,
        Action<CollectionTag> handleAdd)
    {
        // TODO: Move UpdateRelatedList to a helper so we can easily test
        if (tags == null) return;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.CollectionTags.ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null)
            {
                // Remove tag
                series.Metadata.CollectionTags.Remove(existing);
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tag in tags)
        {
            var existingTag = allTags.SingleOrDefault(t => t.Title == tag.Title);
            if (existingTag != null)
            {
                if (series.Metadata.CollectionTags.All(t => t.Title != tag.Title))
                {
                    handleAdd(existingTag);
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.CollectionTag(tag.Id, tag.Title, tag.Summary, tag.Promoted));
            }
        }
    }

    private static void UpdateGenreList(ICollection<GenreTagDto> tags, Series series, IReadOnlyCollection<Genre> allTags, Action<Genre> handleAdd, Action onModified)
    {
        if (tags == null) return;
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Genres.ToList();
        foreach (var existing in existingTags)
        {
            // NOTE: Why don't I use a NormalizedName here (outside of memory pressure from string creation)?
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null)
            {
                // Remove tag
                series.Metadata.Genres.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tagTitle in tags.Select(t => t.Title))
        {
            var normalizedTitle = Parser.Parser.Normalize(tagTitle);
            var existingTag = allTags.SingleOrDefault(t => t.NormalizedTitle == normalizedTitle);
            if (existingTag != null)
            {
                if (series.Metadata.Genres.All(t => t.NormalizedTitle != normalizedTitle))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Genre(tagTitle, false));
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }

    private static void UpdateTagList(ICollection<TagDto> tags, Series series, IReadOnlyCollection<Tag> allTags, Action<Tag> handleAdd, Action onModified)
    {
        if (tags == null) return;

        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Tags.ToList();
        foreach (var existing in existingTags.Where(existing => tags.SingleOrDefault(t => t.Id == existing.Id) == null))
        {
            // Remove tag
            series.Metadata.Tags.Remove(existing);
            isModified = true;
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tagTitle in tags.Select(t => t.Title))
        {
            var normalizedTitle = Parser.Parser.Normalize(tagTitle);
            var existingTag = allTags.SingleOrDefault(t => t.NormalizedTitle.Equals(normalizedTitle));
            if (existingTag != null)
            {
                if (series.Metadata.Tags.All(t => t.NormalizedTitle != normalizedTitle))
                {

                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Tag(tagTitle, false));
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }

    private static void UpdatePeopleList(PersonRole role, ICollection<PersonDto> tags, Series series, IReadOnlyCollection<Person> allTags,
        Action<Person> handleAdd, Action onModified)
    {
        if (tags == null) return;
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.People.Where(p => p.Role == role).ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null) // This needs to check against role
            {
                // Remove tag
                series.Metadata.People.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tag in tags)
        {
            var existingTag = allTags.SingleOrDefault(t => t.Name == tag.Name && t.Role == tag.Role);
            if (existingTag != null)
            {
                if (series.Metadata.People.Where(t => t.Role == tag.Role).All(t => !t.Name.Equals(tag.Name)))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Person(tag.Name, role));
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="user">User with Ratings includes</param>
    /// <param name="updateSeriesRatingDto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateRating(AppUser user, UpdateSeriesRatingDto updateSeriesRatingDto)
    {
        if (user == null)
        {
            _logger.LogError("Cannot update rating of null user");
            return false;
        }

        var userRating =
            await _unitOfWork.UserRepository.GetUserRatingAsync(updateSeriesRatingDto.SeriesId, user.Id) ??
            new AppUserRating();
        try
        {
            userRating.Rating = Math.Clamp(updateSeriesRatingDto.UserRating, 0, 5);
            userRating.Review = updateSeriesRatingDto.UserReview;
            userRating.SeriesId = updateSeriesRatingDto.SeriesId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync()) return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception saving rating");
        }

        await _unitOfWork.RollbackAsync();
        user.Ratings?.Remove(userRating);

        return false;
    }

    public async Task<bool> DeleteMultipleSeries(IList<int> seriesIds)
    {
        try
        {
            var chapterMappings =
                await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(seriesIds.ToArray());

            var allChapterIds = new List<int>();
            foreach (var mapping in chapterMappings)
            {
                allChapterIds.AddRange(mapping.Value);
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(seriesIds);
            var libraryIds = series.Select(s => s.LibraryId);
            var libraries = await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(libraryIds);
            foreach (var library in libraries)
            {
                library.LastModified = DateTime.Now;
                _unitOfWork.LibraryRepository.Update(library);
            }

            _unitOfWork.SeriesRepository.Remove(series);


            if (!_unitOfWork.HasChanges() || !await _unitOfWork.CommitAsync()) return true;

            foreach (var s in series)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(s.Id, s.Name, s.LibraryId), false);
            }

            await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
            await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
            _taskScheduler.CleanupChapters(allChapterIds.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when trying to delete multiple series");
            return false;
        }

        return true;
    }

    /// <summary>
    /// This generates all the arrays needed by the Series Detail page in the UI. It is a specialized API for the unique layout constraints.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId))
            .OrderBy(v => Parser.Parser.MinNumberFromRange(v.Name))
            .ToList();

        // For books, the Name of the Volume is remapped to the actual name of the book, rather than Volume number.
        var processedVolumes = new List<VolumeDto>();
        if (libraryType == LibraryType.Book)
        {
            foreach (var volume in volumes)
            {
                var firstChapter = volume.Chapters.First();
                // On Books, skip volumes that are specials, since these will be shown
                if (firstChapter.IsSpecial) continue;
                RenameVolumeName(firstChapter, volume, libraryType);
                processedVolumes.Add(volume);
            }
        }
        else
        {
            processedVolumes = volumes.Where(v => v.Number > 0).ToList();
            processedVolumes.ForEach(v => v.Name = $"Volume {v.Name}");
        }

        var specials = new List<ChapterDto>();
        var chapters = volumes.SelectMany(v => v.Chapters.Select(c =>
        {
            if (v.Number == 0) return c;
            c.VolumeTitle = v.Name;
            return c;
        }).OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default));

        foreach (var chapter in chapters)
        {
            chapter.Title = FormatChapterTitle(chapter, libraryType);
            if (!chapter.IsSpecial) continue;

            if (!string.IsNullOrEmpty(chapter.TitleName)) chapter.Title = chapter.TitleName;
            specials.Add(chapter);
        }

        // Don't show chapter 0 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
        IEnumerable<ChapterDto> retChapters;
        if (libraryType == LibraryType.Book)
        {
            retChapters = Array.Empty<ChapterDto>();
        } else
        {
            retChapters = chapters
                .Where(ShouldIncludeChapter);
        }

        var storylineChapters = volumes
            .Where(v => v.Number == 0)
            .SelectMany(v => v.Chapters.Where(c => !c.IsSpecial))
            .OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default);

        // When there's chapters without a volume number revert to chapter sorting only as opposed to volume then chapter
        if (storylineChapters.Any()) {
            retChapters = retChapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default);
        }

        return new SeriesDetailDto()
        {
            Specials = specials,
            Chapters = retChapters,
            Volumes = processedVolumes,
            StorylineChapters = storylineChapters
        };
    }

    /// <summary>
    /// Should we show the given chapter on the UI. We only show non-specials and non-zero chapters.
    /// </summary>
    /// <param name="chapter"></param>
    /// <returns></returns>
    private static bool ShouldIncludeChapter(ChapterDto chapter)
    {
        return !chapter.IsSpecial && !chapter.Number.Equals(Parser.Parser.DefaultChapter);
    }

    public static void RenameVolumeName(ChapterDto firstChapter, VolumeDto volume, LibraryType libraryType)
    {
        if (libraryType == LibraryType.Book)
        {
            if (string.IsNullOrEmpty(firstChapter.TitleName))
            {
                if (firstChapter.Range.Equals(Parser.Parser.DefaultVolume)) return;
                var title = Path.GetFileNameWithoutExtension(firstChapter.Range);
                if (string.IsNullOrEmpty(title)) return;
                volume.Name += $" - {title}";
            }
            else
            {
                volume.Name += $" - {firstChapter.TitleName}";
            }

            return;
        }

        volume.Name = $"Volume {volume.Name}";
    }


    private static string FormatChapterTitle(bool isSpecial, LibraryType libraryType, string chapterTitle, bool withHash)
    {
        if (isSpecial)
        {
            return Parser.Parser.CleanSpecialTitle(chapterTitle);
        }

        var hashSpot = withHash ? "#" : string.Empty;
        return libraryType switch
        {
            LibraryType.Book => $"Book {chapterTitle}",
            LibraryType.Comic => $"Issue {hashSpot}{chapterTitle}",
            LibraryType.Manga => $"Chapter {chapterTitle}",
            _ => "Chapter "
        };
    }

    public static string FormatChapterTitle(ChapterDto chapter, LibraryType libraryType, bool withHash = true)
    {
        return FormatChapterTitle(chapter.IsSpecial, libraryType, chapter.Title, withHash);
    }

    public static string FormatChapterTitle(Chapter chapter, LibraryType libraryType, bool withHash = true)
    {
        return FormatChapterTitle(chapter.IsSpecial, libraryType, chapter.Title, withHash);
    }

    public static string FormatChapterName(LibraryType libraryType, bool withHash = false)
    {
        return libraryType switch
        {
            LibraryType.Manga => "Chapter",
            LibraryType.Comic => withHash ? "Issue #" : "Issue",
            LibraryType.Book => "Book",
            _ => "Chapter"
        };
    }
}
