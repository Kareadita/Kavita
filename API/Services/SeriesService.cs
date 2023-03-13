﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
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
    Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId);
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
    public static Chapter? GetFirstChapterForMetadata(Series series, bool isBookLibrary)
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
            if (series == null) return false;
            var allCollectionTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).ToList();
            var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToList();
            var allPeople = (await _unitOfWork.PersonRepository.GetAllPeople()).ToList();
            var allTags = (await _unitOfWork.TagRepository.GetAllTagsAsync()).ToList();

            series.Metadata ??= DbFactory.SeriesMetadata((updateSeriesMetadataDto.CollectionTags ?? new List<CollectionTagDto>())
                .Select(dto => DbFactory.CollectionTag(dto.Id, dto.Title, dto.Summary, dto.Promoted)).ToList());

            if (series.Metadata.AgeRating != updateSeriesMetadataDto.SeriesMetadata.AgeRating)
            {
                series.Metadata.AgeRating = updateSeriesMetadataDto.SeriesMetadata.AgeRating;
                series.Metadata.AgeRatingLocked = true;
            }

            if (NumberHelper.IsValidYear(updateSeriesMetadataDto.SeriesMetadata.ReleaseYear) && series.Metadata.ReleaseYear != updateSeriesMetadataDto.SeriesMetadata.ReleaseYear)
            {
                series.Metadata.ReleaseYear = updateSeriesMetadataDto.SeriesMetadata.ReleaseYear;
                series.Metadata.ReleaseYearLocked = true;
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
                series.Metadata.Summary = updateSeriesMetadataDto.SeriesMetadata?.Summary.Trim() ?? string.Empty;
                series.Metadata.SummaryLocked = true;
            }

            if (series.Metadata.Language != updateSeriesMetadataDto.SeriesMetadata?.Language)
            {
                series.Metadata.Language = updateSeriesMetadataDto.SeriesMetadata?.Language ?? string.Empty;
                series.Metadata.LanguageLocked = true;
            }

            series.Metadata.CollectionTags ??= new List<CollectionTag>();
            UpdateCollectionsList(updateSeriesMetadataDto.CollectionTags, series, allCollectionTags, (tag) =>
            {
                series.Metadata.CollectionTags.Add(tag);
            });

            series.Metadata.Genres ??= new List<Genre>();
            GenreHelper.UpdateGenreList(updateSeriesMetadataDto.SeriesMetadata?.Genres, series, allGenres, (genre) =>
            {
                series.Metadata.Genres.Add(genre);
            }, () => series.Metadata.GenresLocked = true);

            series.Metadata.Tags ??= new List<Tag>();
            TagHelper.UpdateTagList(updateSeriesMetadataDto.SeriesMetadata?.Tags, series, allTags, (tag) =>
            {
                series.Metadata.Tags.Add(tag);
            }, () => series.Metadata.TagsLocked = true);

            void HandleAddPerson(Person person)
            {
                PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                allPeople.Add(person);
            }

            series.Metadata.People ??= new List<Person>();
            PersonHelper.UpdatePeopleList(PersonRole.Writer, updateSeriesMetadataDto.SeriesMetadata!.Writers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.WriterLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Character, updateSeriesMetadataDto.SeriesMetadata.Characters, series, allPeople,
                HandleAddPerson,  () => series.Metadata.CharacterLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Colorist, updateSeriesMetadataDto.SeriesMetadata.Colorists, series, allPeople,
                HandleAddPerson,  () => series.Metadata.ColoristLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Editor, updateSeriesMetadataDto.SeriesMetadata.Editors, series, allPeople,
                HandleAddPerson,  () => series.Metadata.EditorLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Inker, updateSeriesMetadataDto.SeriesMetadata.Inkers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.InkerLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Letterer, updateSeriesMetadataDto.SeriesMetadata.Letterers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.LettererLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Penciller, updateSeriesMetadataDto.SeriesMetadata.Pencillers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.PencillerLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Publisher, updateSeriesMetadataDto.SeriesMetadata.Publishers, series, allPeople,
                HandleAddPerson,  () => series.Metadata.PublisherLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.Translator, updateSeriesMetadataDto.SeriesMetadata.Translators, series, allPeople,
                HandleAddPerson,  () => series.Metadata.TranslatorLocked = true);
            PersonHelper.UpdatePeopleList(PersonRole.CoverArtist, updateSeriesMetadataDto.SeriesMetadata.CoverArtists, series, allPeople,
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
            series.Metadata.ReleaseYearLocked = updateSeriesMetadataDto.SeriesMetadata.ReleaseYearLocked;

            if (!_unitOfWork.HasChanges())
            {
                return true;
            }

            await _unitOfWork.CommitAsync();

            // Trigger code to cleanup tags, collections, people, etc
            await _taskScheduler.CleanupDbEntries();

            if (updateSeriesMetadataDto.CollectionTags != null)
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


    public static void UpdateCollectionsList(ICollection<CollectionTagDto>? tags, Series series, IReadOnlyCollection<CollectionTag> allTags,
        Action<CollectionTag> handleAdd)
    {
        // TODO: Move UpdateCollectionsList to a helper so we can easily test
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

    /// <summary>
    ///
    /// </summary>
    /// <param name="user">User with Ratings includes</param>
    /// <param name="updateSeriesRatingDto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateRating(AppUser? user, UpdateSeriesRatingDto updateSeriesRatingDto)
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
                library.UpdateLastModified();
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
        var libraryIds = _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);
        if (!libraryIds.Contains(series.LibraryId))
            throw new UnauthorizedAccessException("User does not have access to the library this series belongs to");

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user!.AgeRestriction != AgeRating.NotApplicable)
        {
            var seriesMetadata = await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
            if (seriesMetadata!.AgeRating > user.AgeRestriction)
                throw new UnauthorizedAccessException("User is not allowed to view this series due to age restrictions");
        }

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId))
            .OrderBy(v => Tasks.Scanner.Parser.Parser.MinNumberFromRange(v.Name))
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
        }).OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default)).ToList();

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
            .OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default)
            .ToList();

        // When there's chapters without a volume number revert to chapter sorting only as opposed to volume then chapter
        if (storylineChapters.Any()) {
            retChapters = retChapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default);
        }

        return new SeriesDetailDto()
        {
            Specials = specials,
            Chapters = retChapters,
            Volumes = processedVolumes,
            StorylineChapters = storylineChapters,
            TotalCount = chapters.Count,
            UnreadCount = chapters.Count(c => c.Pages > 0 && c.PagesRead < c.Pages)
        };
    }

    /// <summary>
    /// Should we show the given chapter on the UI. We only show non-specials and non-zero chapters.
    /// </summary>
    /// <param name="chapter"></param>
    /// <returns></returns>
    private static bool ShouldIncludeChapter(ChapterDto chapter)
    {
        return !chapter.IsSpecial && !chapter.Number.Equals(Tasks.Scanner.Parser.Parser.DefaultChapter);
    }

    public static void RenameVolumeName(ChapterDto firstChapter, VolumeDto volume, LibraryType libraryType)
    {
        if (libraryType == LibraryType.Book)
        {
            if (string.IsNullOrEmpty(firstChapter.TitleName))
            {
                if (firstChapter.Range.Equals(Tasks.Scanner.Parser.Parser.DefaultVolume)) return;
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


    private static string FormatChapterTitle(bool isSpecial, LibraryType libraryType, string? chapterTitle, bool withHash)
    {
        if (string.IsNullOrEmpty(chapterTitle)) throw new ArgumentException("Chapter Title cannot be null");

        if (isSpecial)
        {
            return Tasks.Scanner.Parser.Parser.CleanSpecialTitle(chapterTitle);
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

    /// <summary>
    /// Returns all related series against the passed series Id
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId)
    {
        return await _unitOfWork.SeriesRepository.GetRelatedSeries(userId, seriesId);
    }

    /// <summary>
    /// Update the relations attached to the Series. Does not generate associated Sequel/Prequel pairs on target series.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId, SeriesIncludes.Related);
        if (series == null) return false;

        UpdateRelationForKind(dto.Adaptations, series.Relations.Where(r => r.RelationKind == RelationKind.Adaptation).ToList(), series, RelationKind.Adaptation);
        UpdateRelationForKind(dto.Characters, series.Relations.Where(r => r.RelationKind == RelationKind.Character).ToList(), series, RelationKind.Character);
        UpdateRelationForKind(dto.Contains, series.Relations.Where(r => r.RelationKind == RelationKind.Contains).ToList(), series, RelationKind.Contains);
        UpdateRelationForKind(dto.Others, series.Relations.Where(r => r.RelationKind == RelationKind.Other).ToList(), series, RelationKind.Other);
        UpdateRelationForKind(dto.SideStories, series.Relations.Where(r => r.RelationKind == RelationKind.SideStory).ToList(), series, RelationKind.SideStory);
        UpdateRelationForKind(dto.SpinOffs, series.Relations.Where(r => r.RelationKind == RelationKind.SpinOff).ToList(), series, RelationKind.SpinOff);
        UpdateRelationForKind(dto.AlternativeSettings, series.Relations.Where(r => r.RelationKind == RelationKind.AlternativeSetting).ToList(), series, RelationKind.AlternativeSetting);
        UpdateRelationForKind(dto.AlternativeVersions, series.Relations.Where(r => r.RelationKind == RelationKind.AlternativeVersion).ToList(), series, RelationKind.AlternativeVersion);
        UpdateRelationForKind(dto.Doujinshis, series.Relations.Where(r => r.RelationKind == RelationKind.Doujinshi).ToList(), series, RelationKind.Doujinshi);
        UpdateRelationForKind(dto.Prequels, series.Relations.Where(r => r.RelationKind == RelationKind.Prequel).ToList(), series, RelationKind.Prequel);
        UpdateRelationForKind(dto.Sequels, series.Relations.Where(r => r.RelationKind == RelationKind.Sequel).ToList(), series, RelationKind.Sequel);
        UpdateRelationForKind(dto.Editions, series.Relations.Where(r => r.RelationKind == RelationKind.Edition).ToList(), series, RelationKind.Edition);

        if (!_unitOfWork.HasChanges()) return true;
        return await _unitOfWork.CommitAsync();
    }


    /// <summary>
    /// Applies the provided list to the series. Adds new relations and removes deleted relations.
    /// </summary>
    /// <param name="dtoTargetSeriesIds"></param>
    /// <param name="adaptations"></param>
    /// <param name="series"></param>
    /// <param name="kind"></param>
    private void UpdateRelationForKind(ICollection<int> dtoTargetSeriesIds, IEnumerable<SeriesRelation> adaptations, Series series, RelationKind kind)
    {
        foreach (var adaptation in adaptations.Where(adaptation => !dtoTargetSeriesIds.Contains(adaptation.TargetSeriesId)))
        {
            // If the seriesId isn't in dto, it means we've removed or reclassified
            series.Relations.Remove(adaptation);
        }

        // At this point, we only have things to add
        foreach (var targetSeriesId in dtoTargetSeriesIds)
        {
            // This ensures we don't allow any duplicates to be added
            if (series.Relations.SingleOrDefault(r =>
                    r.RelationKind == kind && r.TargetSeriesId == targetSeriesId) !=
                null) continue;

            series.Relations.Add(new SeriesRelation()
            {
                Series = series,
                SeriesId = series.Id,
                TargetSeriesId = targetSeriesId,
                RelationKind = kind
            });
            _unitOfWork.SeriesRepository.Update(series);
        }
    }
}
