using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

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

            if (series.Metadata == null)
            {
                series.Metadata = DbFactory.SeriesMetadata(updateSeriesMetadataDto.CollectionTags
                    .Select(dto => DbFactory.CollectionTag(dto.Id, dto.Title, dto.Summary, dto.Promoted)).ToList());
            }
            else
            {
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

                if (series.Metadata.Summary != updateSeriesMetadataDto.SeriesMetadata.Summary.Trim())
                {
                    series.Metadata.Summary = updateSeriesMetadataDto.SeriesMetadata?.Summary.Trim();
                    series.Metadata.SummaryLocked = true;
                }


                series.Metadata.CollectionTags ??= new List<CollectionTag>();
                UpdateRelatedList(updateSeriesMetadataDto.CollectionTags, series, allCollectionTags, (tag) =>
                {
                    series.Metadata.CollectionTags.Add(tag);
                });

                series.Metadata.Genres ??= new List<Genre>();
                UpdateGenreList(updateSeriesMetadataDto.SeriesMetadata.Genres, series, allGenres, (genre) =>
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

                if (!updateSeriesMetadataDto.SeriesMetadata.AgeRatingLocked) series.Metadata.AgeRatingLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.PublicationStatusLocked) series.Metadata.PublicationStatusLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.LanguageLocked) series.Metadata.LanguageLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.GenresLocked) series.Metadata.GenresLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.TagsLocked) series.Metadata.TagsLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.CharacterLocked) series.Metadata.CharacterLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.ColoristLocked) series.Metadata.ColoristLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.EditorLocked) series.Metadata.EditorLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.InkerLocked) series.Metadata.InkerLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.LettererLocked) series.Metadata.LettererLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.PencillerLocked) series.Metadata.PencillerLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.PublisherLocked) series.Metadata.PublisherLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.TranslatorLocked) series.Metadata.TranslatorLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.CoverArtistLocked) series.Metadata.CoverArtistLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.WriterLocked) series.Metadata.WriterLocked = false;
                if (!updateSeriesMetadataDto.SeriesMetadata.SummaryLocked) series.Metadata.SummaryLocked = false;

            }

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
                    MessageFactory.ScanSeriesEvent(series.Id, series.Name), false);

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

    // TODO: Move this to a helper so we can easily test
    private static void UpdateRelatedList(ICollection<CollectionTagDto> tags, Series series, IReadOnlyCollection<CollectionTag> allTags,
        Action<CollectionTag> handleAdd)
    {
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
                    //newTags.Add(existingTag);
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
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Genres.ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null)
            {
                // Remove tag
                series.Metadata.Genres.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tag in tags)
        {
            var existingTag = allTags.SingleOrDefault(t => t.Title == tag.Title);
            if (existingTag != null)
            {
                if (series.Metadata.Genres.All(t => t.Title != tag.Title))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Genre(tag.Title, false));
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
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Tags.ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null)
            {
                // Remove tag
                series.Metadata.Tags.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tag in tags)
        {
            var existingTag = allTags.SingleOrDefault(t => t.Title == tag.Title);
            if (existingTag != null)
            {
                if (series.Metadata.Tags.All(t => t.Title != tag.Title))
                {

                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Tag(tag.Title, false));
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
                if (series.Metadata.People.All(t => t.Name != tag.Name && t.Role == tag.Role))
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
            .OrderBy(v => float.Parse(v.Name))
            .ToList();
        var chapters = volumes.SelectMany(v => v.Chapters).ToList();

        // For books, the Name of the Volume is remapped to the actual name of the book, rather than Volume number.
        var processedVolumes = new List<VolumeDto>();
        if (libraryType == LibraryType.Book)
        {
            foreach (var volume in volumes)
            {
                var firstChapter = volume.Chapters.First();
                // On Books, skip volumes that are specials, since these will be shown
                if (firstChapter.IsSpecial) continue;
                if (string.IsNullOrEmpty(firstChapter.TitleName))
                {
                    if (!firstChapter.Range.Equals(Parser.Parser.DefaultVolume))
                    {
                        var title = Path.GetFileNameWithoutExtension(firstChapter.Range);
                        if (string.IsNullOrEmpty(title)) continue;
                        volume.Name += $" - {title}";
                    }
                }
                else
                {
                    volume.Name += $" - {firstChapter.TitleName}";
                }
                processedVolumes.Add(volume);
            }
        }
        else
        {
            processedVolumes = volumes.Where(v => v.Number > 0).ToList();
        }


        var specials = new List<ChapterDto>();
        foreach (var chapter in chapters.Where(c => c.IsSpecial))
        {
            chapter.Title = Parser.Parser.CleanSpecialTitle(chapter.Title);
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
                .Where(ShouldIncludeChapter)
                .OrderBy(c => float.Parse(c.Number), new ChapterSortComparer());
        }



        return new SeriesDetailDto()
        {
            Specials = specials,
            Chapters = retChapters,
            Volumes = processedVolumes,
            StorylineChapters = volumes
                .Where(v => v.Number == 0)
                .SelectMany(v => v.Chapters)
                .OrderBy(c => float.Parse(c.Number), new ChapterSortComparer())

        };
    }

    /// <summary>
    /// Should we show the given chapter on the UI. We only show non-specials and non-zero chapters.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    private static bool ShouldIncludeChapter(ChapterDto c)
    {
        return !c.IsSpecial && !c.Number.Equals(Parser.Parser.DefaultChapter);
    }
}
