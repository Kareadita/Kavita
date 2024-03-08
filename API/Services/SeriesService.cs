using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Constants;
using API.Controllers;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using EasyCaching.Core;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto);
    Task<bool> UpdateRating(AppUser user, UpdateSeriesRatingDto updateSeriesRatingDto);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds);
    Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId);
    Task<string> FormatChapterTitle(int userId, ChapterDto chapter, LibraryType libraryType, bool withHash = true);
    Task<string> FormatChapterTitle(int userId, Chapter chapter, LibraryType libraryType, bool withHash = true);

    Task<string> FormatChapterTitle(int userId, bool isSpecial, LibraryType libraryType, string? chapterTitle,
        bool withHash);
    Task<string> FormatChapterName(int userId, LibraryType libraryType, bool withHash = false);
    Task<NextExpectedChapterDto> GetEstimatedChapterCreationDate(int seriesId, int userId);
}

public class SeriesService : ISeriesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<SeriesService> _logger;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILocalizationService _localizationService;

    private readonly NextExpectedChapterDto _emptyExpectedChapter = new NextExpectedChapterDto
    {
        ExpectedDate = null,
        ChapterNumber = 0,
        VolumeNumber = Parser.LooseLeafVolumeNumber
    };

    public SeriesService(IUnitOfWork unitOfWork, IEventHub eventHub, ITaskScheduler taskScheduler,
        ILogger<SeriesService> logger, IScrobblingService scrobblingService, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _taskScheduler = taskScheduler;
        _logger = logger;
        _scrobblingService = scrobblingService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Returns the first chapter for a series to extract metadata from (ie Summary, etc)
    /// </summary>
    /// <param name="series">The full series with all volumes and chapters on it</param>
    /// <returns></returns>
    public static Chapter? GetFirstChapterForMetadata(Series series)
    {
        var sortedVolumes = series.Volumes
            .Where(v => v.MinNumber.IsNot(Parser.LooseLeafVolumeNumber))
            .OrderBy(v => v.MinNumber);
        var minVolumeNumber = sortedVolumes.MinBy(v => v.MinNumber);


        var allChapters = series.Volumes
            .SelectMany(v => v.Chapters.OrderBy(c => c.MinNumber, ChapterSortComparerDefaultLast.Default))
            .ToList();
        var minChapter = allChapters
            .FirstOrDefault();

        if (minVolumeNumber != null && minChapter != null &&
            (minChapter.MinNumber >= minVolumeNumber.MinNumber || minChapter.MinNumber.Is(Parser.DefaultChapterNumber)))
        {
            return minVolumeNumber.Chapters.MinBy(c => c.MinNumber, ChapterSortComparerDefaultLast.Default);
        }

        return minChapter;
    }

    /// <summary>
    /// Updates the Series Metadata.
    /// </summary>
    /// <param name="updateSeriesMetadataDto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
    {
        try
        {
            var seriesId = updateSeriesMetadataDto.SeriesMetadata.SeriesId;
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            if (series == null) return false;

            series.Metadata ??= new SeriesMetadataBuilder()
                .WithCollectionTags(updateSeriesMetadataDto.CollectionTags.Select(dto =>
                    new CollectionTagBuilder(dto.Title)
                        .WithId(dto.Id)
                        .WithSummary(dto.Summary)
                        .WithIsPromoted(dto.Promoted)
                        .Build()).ToList())
                .Build();

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

            if (string.IsNullOrEmpty(updateSeriesMetadataDto.SeriesMetadata?.WebLinks))
            {
                series.Metadata.WebLinks = string.Empty;
            } else
            {
                series.Metadata.WebLinks = string.Join(",", updateSeriesMetadataDto.SeriesMetadata?.WebLinks
                    .Split(",")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Trim())!
                );
            }


            if (updateSeriesMetadataDto.CollectionTags.Any())
            {
                var allCollectionTags = (await _unitOfWork.CollectionTagRepository
                    .GetAllTagsByNamesAsync(updateSeriesMetadataDto.CollectionTags.Select(t => Parser.Normalize(t.Title)))).ToList();
                series.Metadata.CollectionTags ??= new List<CollectionTag>();
                UpdateCollectionsList(updateSeriesMetadataDto.CollectionTags, series, allCollectionTags, tag =>
                {
                    series.Metadata.CollectionTags.Add(tag);
                });
            }


            if (updateSeriesMetadataDto.SeriesMetadata?.Genres != null &&
                updateSeriesMetadataDto.SeriesMetadata.Genres.Any())
            {
                var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(updateSeriesMetadataDto.SeriesMetadata.Genres.Select(t => Parser.Normalize(t.Title)))).ToList();
                series.Metadata.Genres ??= new List<Genre>();
                GenreHelper.UpdateGenreList(updateSeriesMetadataDto.SeriesMetadata?.Genres, series, allGenres, genre =>
                {
                    series.Metadata.Genres.Add(genre);
                }, () => series.Metadata.GenresLocked = true);
            }


            if (updateSeriesMetadataDto.SeriesMetadata?.Tags != null && updateSeriesMetadataDto.SeriesMetadata.Tags.Any())
            {
                var allTags = (await _unitOfWork.TagRepository
                    .GetAllTagsByNameAsync(updateSeriesMetadataDto.SeriesMetadata.Tags.Select(t => Parser.Normalize(t.Title))))
                    .ToList();
                series.Metadata.Tags ??= new List<Tag>();
                TagHelper.UpdateTagList(updateSeriesMetadataDto.SeriesMetadata?.Tags, series, allTags, tag =>
                {
                    series.Metadata.Tags.Add(tag);
                }, () => series.Metadata.TagsLocked = true);
            }


            if (PersonHelper.HasAnyPeople(updateSeriesMetadataDto.SeriesMetadata))
            {
                void HandleAddPerson(Person person)
                {
                    PersonHelper.AddPersonIfNotExists(series.Metadata.People, person);
                }

                series.Metadata.People ??= new List<Person>();
                var allWriters = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Writer,
                    updateSeriesMetadataDto.SeriesMetadata!.Writers.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Writer, updateSeriesMetadataDto.SeriesMetadata!.Writers, series, allWriters.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.WriterLocked = true);

                var allCharacters = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Character,
                    updateSeriesMetadataDto.SeriesMetadata!.Characters.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Character, updateSeriesMetadataDto.SeriesMetadata.Characters, series, allCharacters.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.CharacterLocked = true);

                var allColorists = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Colorist,
                    updateSeriesMetadataDto.SeriesMetadata!.Colorists.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Colorist, updateSeriesMetadataDto.SeriesMetadata.Colorists, series, allColorists.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.ColoristLocked = true);

                var allEditors = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Editor,
                    updateSeriesMetadataDto.SeriesMetadata!.Editors.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Editor, updateSeriesMetadataDto.SeriesMetadata.Editors, series, allEditors.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.EditorLocked = true);

                var allInkers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Inker,
                    updateSeriesMetadataDto.SeriesMetadata!.Inkers.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Inker, updateSeriesMetadataDto.SeriesMetadata.Inkers, series, allInkers.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.InkerLocked = true);

                var allLetterers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Letterer,
                    updateSeriesMetadataDto.SeriesMetadata!.Letterers.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Letterer, updateSeriesMetadataDto.SeriesMetadata.Letterers, series, allLetterers.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.LettererLocked = true);

                var allPencillers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Penciller,
                    updateSeriesMetadataDto.SeriesMetadata!.Pencillers.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Penciller, updateSeriesMetadataDto.SeriesMetadata.Pencillers, series, allPencillers.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.PencillerLocked = true);

                var allPublishers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Publisher,
                    updateSeriesMetadataDto.SeriesMetadata!.Publishers.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Publisher, updateSeriesMetadataDto.SeriesMetadata.Publishers, series, allPublishers.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.PublisherLocked = true);

                var allTranslators = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Translator,
                    updateSeriesMetadataDto.SeriesMetadata!.Translators.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.Translator, updateSeriesMetadataDto.SeriesMetadata.Translators, series, allTranslators.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.TranslatorLocked = true);

                var allCoverArtists = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.CoverArtist,
                    updateSeriesMetadataDto.SeriesMetadata!.CoverArtists.Select(p => Parser.Normalize(p.Name)));
                PersonHelper.UpdatePeopleList(PersonRole.CoverArtist, updateSeriesMetadataDto.SeriesMetadata.CoverArtists, series, allCoverArtists.AsReadOnly(),
                    HandleAddPerson,  () => series.Metadata.CoverArtistLocked = true);
            }

            if (updateSeriesMetadataDto.SeriesMetadata != null)
            {
                series.Metadata.AgeRatingLocked = updateSeriesMetadataDto.SeriesMetadata.AgeRatingLocked;
                series.Metadata.PublicationStatusLocked = updateSeriesMetadataDto.SeriesMetadata.PublicationStatusLocked;
                series.Metadata.LanguageLocked = updateSeriesMetadataDto.SeriesMetadata.LanguageLocked;
                series.Metadata.GenresLocked = updateSeriesMetadataDto.SeriesMetadata.GenresLocked;
                series.Metadata.TagsLocked = updateSeriesMetadataDto.SeriesMetadata.TagsLocked;
                series.Metadata.CharacterLocked = updateSeriesMetadataDto.SeriesMetadata.CharacterLocked;
                series.Metadata.ColoristLocked = updateSeriesMetadataDto.SeriesMetadata.ColoristLocked;
                series.Metadata.EditorLocked = updateSeriesMetadataDto.SeriesMetadata.EditorLocked;
                series.Metadata.InkerLocked = updateSeriesMetadataDto.SeriesMetadata.InkerLocked;
                series.Metadata.LettererLocked = updateSeriesMetadataDto.SeriesMetadata.LettererLocked;
                series.Metadata.PencillerLocked = updateSeriesMetadataDto.SeriesMetadata.PencillerLocked;
                series.Metadata.PublisherLocked = updateSeriesMetadataDto.SeriesMetadata.PublisherLocked;
                series.Metadata.TranslatorLocked = updateSeriesMetadataDto.SeriesMetadata.TranslatorLocked;
                series.Metadata.CoverArtistLocked = updateSeriesMetadataDto.SeriesMetadata.CoverArtistLocked;
                series.Metadata.WriterLocked = updateSeriesMetadataDto.SeriesMetadata.WriterLocked;
                series.Metadata.SummaryLocked = updateSeriesMetadataDto.SeriesMetadata.SummaryLocked;
                series.Metadata.ReleaseYearLocked = updateSeriesMetadataDto.SeriesMetadata.ReleaseYearLocked;
            }

            if (!_unitOfWork.HasChanges())
            {
                return true;
            }

            await _unitOfWork.CommitAsync();

            // Trigger code to cleanup tags, collections, people, etc
            try
            {
                await _taskScheduler.CleanupDbEntries();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue cleaning up DB entries. This may happen if Komf is spamming updates. Nightly cleanup will work");
            }

            if (updateSeriesMetadataDto.CollectionTags == null) return true;
            foreach (var tag in updateSeriesMetadataDto.CollectionTags)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesAddedToCollection,
                    MessageFactory.SeriesAddedToCollectionEvent(tag.Id, seriesId), false);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating metadata");
            await _unitOfWork.RollbackAsync();
        }

        return false;
    }


    private static void UpdateCollectionsList(ICollection<CollectionTagDto>? tags, Series series, IReadOnlyCollection<CollectionTag> allTags,
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
                handleAdd(new CollectionTagBuilder(tag.Title)
                    .WithId(tag.Id)
                    .WithSummary(tag.Summary)
                    .WithIsPromoted(tag.Promoted)
                    .Build());
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
            userRating.Rating = Math.Clamp(updateSeriesRatingDto.UserRating, 0f, 5f);
            userRating.HasBeenRated = true;
            userRating.SeriesId = updateSeriesRatingDto.SeriesId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                BackgroundJob.Enqueue(() =>
                    _scrobblingService.ScrobbleRatingUpdate(user.Id, updateSeriesRatingDto.SeriesId,
                        userRating.Rating));
                return true;
            }
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

            var libraryIds = series.Select(s => s.LibraryId);
            var libraries = await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(libraryIds);
            foreach (var library in libraries)
            {
                library.UpdateLastModified();
                _unitOfWork.LibraryRepository.Update(library);
            }
            await _unitOfWork.CommitAsync();


            foreach (var s in series)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(s.Id, s.Name, s.LibraryId), false);
            }

            await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
            await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
            _taskScheduler.CleanupChapters(allChapterIds.ToArray());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when trying to delete multiple series");
            return false;
        }
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
        if (series == null) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));
        var libraryIds = _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);
        if (!libraryIds.Contains(series.LibraryId))
            throw new UnauthorizedAccessException("user-no-access-library-from-series");

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user!.AgeRestriction != AgeRating.NotApplicable)
        {
            var seriesMetadata = await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
            if (seriesMetadata!.AgeRating > user.AgeRestriction)
                throw new UnauthorizedAccessException("series-restricted-age-restriction");
        }


        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var bookTreatment = libraryType is LibraryType.Book or LibraryType.LightNovel;
        var volumeLabel = await _localizationService.Translate(userId, "volume-num", string.Empty);
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId);

        // For books, the Name of the Volume is remapped to the actual name of the book, rather than Volume number.
        var processedVolumes = new List<VolumeDto>();
        foreach (var volume in volumes)
        {
            if (volume.IsLooseLeaf() || volume.IsSpecial())
            {
                continue;
            }

            volume.Chapters = volume.Chapters
                .OrderBy(d => d.MinNumber, ChapterSortComparerDefaultLast.Default)
                .ToList();

            if (RenameVolumeName(volume, libraryType, volumeLabel) || (bookTreatment && !volume.IsSpecial()))
            {
                processedVolumes.Add(volume);
            }
        }

        var specials = new List<ChapterDto>();
        // Why isn't this doing a check if chapter is not special as it wont get included
        var chapters = volumes
            .SelectMany(v => v.Chapters
                .Select(c =>
                {
                    if (v.IsLooseLeaf() || v.IsSpecial()) return c;
                    c.VolumeTitle = v.Name;
                    return c;
                })
                .OrderBy(c => c.SortOrder))
                .ToList();

        foreach (var chapter in chapters)
        {
            if (!string.IsNullOrEmpty(chapter.TitleName)) chapter.Title = chapter.TitleName;
            else chapter.Title = await FormatChapterTitle(userId, chapter, libraryType);

            if (!chapter.IsSpecial) continue;
            specials.Add(chapter);
        }

        // Don't show chapter 0 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
        IEnumerable<ChapterDto> retChapters = bookTreatment ? Array.Empty<ChapterDto>() : chapters.Where(ShouldIncludeChapter);

        var storylineChapters = volumes
            .WhereLooseLeaf()
            .SelectMany(v => v.Chapters.Where(c => !c.IsSpecial))
            .OrderBy(c => c.SortOrder)
            .ToList();

        // When there's chapters without a volume number revert to chapter sorting only as opposed to volume then chapter
        if (storylineChapters.Count > 0) {
            retChapters = retChapters.OrderBy(c => c.SortOrder, ChapterSortComparerDefaultLast.Default);
        }

        return new SeriesDetailDto
        {
            Specials = specials,
            Chapters = retChapters,
            Volumes = processedVolumes,
            StorylineChapters = storylineChapters,
            TotalCount = chapters.Count,
            UnreadCount = chapters.Count(c => c.Pages > 0 && c.PagesRead < c.Pages),
        };
    }

    /// <summary>
    /// Should we show the given chapter on the UI. We only show non-specials and non-zero chapters.
    /// </summary>
    /// <param name="chapter"></param>
    /// <returns></returns>
    private static bool ShouldIncludeChapter(ChapterDto chapter)
    {
        return !chapter.IsSpecial && chapter.MinNumber.IsNot(Parser.DefaultChapterNumber);
    }

    public static bool RenameVolumeName(VolumeDto volume, LibraryType libraryType, string volumeLabel = "Volume")
    {
        // TODO: Move this into DB (not sure how because of localization and lookups)
        if (libraryType is LibraryType.Book or LibraryType.LightNovel)
        {
            var firstChapter = volume.Chapters.First();
            // On Books, skip volumes that are specials, since these will be shown
            if (firstChapter.IsSpecial) return false;
            if (string.IsNullOrEmpty(firstChapter.TitleName))
            {
                if (firstChapter.Range.Equals(Parser.LooseLeafVolume)) return false;
                var title = Path.GetFileNameWithoutExtension(firstChapter.Range);
                if (string.IsNullOrEmpty(title)) return false;
                volume.Name += $" - {title}"; // OPDS smart list 7 (just pdfs) triggered this
            }
            else if (!volume.IsLooseLeaf())
            {
                // If the titleName has Volume inside it, let's just send that back?
                volume.Name = firstChapter.TitleName;
            }

            return true;
        }

        volume.Name = $"{volumeLabel.Trim()} {volume.Name}".Trim();
        return true;
    }


    public async Task<string> FormatChapterTitle(int userId, bool isSpecial, LibraryType libraryType, string? chapterTitle, bool withHash)
    {
        if (string.IsNullOrEmpty(chapterTitle)) throw new ArgumentException("Chapter Title cannot be null");

        if (isSpecial)
        {
            return Parser.CleanSpecialTitle(chapterTitle);
        }

        var hashSpot = withHash ? "#" : string.Empty;
        return libraryType switch
        {
            LibraryType.Book => await _localizationService.Translate(userId, "book-num", chapterTitle),
            LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", chapterTitle),
            LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", hashSpot, chapterTitle),
            LibraryType.Manga => await _localizationService.Translate(userId, "chapter-num", chapterTitle),
            _ => await _localizationService.Translate(userId, "chapter-num", ' ')
        };
    }

    public async Task<string> FormatChapterTitle(int userId, ChapterDto chapter, LibraryType libraryType, bool withHash = true)
    {
        return await FormatChapterTitle(userId, chapter.IsSpecial, libraryType, chapter.Title, withHash);
    }

    public async Task<string> FormatChapterTitle(int userId, Chapter chapter, LibraryType libraryType, bool withHash = true)
    {
        return await FormatChapterTitle(userId, chapter.IsSpecial, libraryType, chapter.Title, withHash);
    }

    public async Task<string> FormatChapterName(int userId, LibraryType libraryType, bool withHash = false)
    {
        var hashSpot = withHash ? "#" : string.Empty;
        return (libraryType switch
        {
            LibraryType.Book => await _localizationService.Translate(userId, "book-num", string.Empty),
            LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", string.Empty),
            LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", hashSpot, string.Empty),
            LibraryType.Manga => await _localizationService.Translate(userId, "chapter-num", string.Empty),
            _ => await _localizationService.Translate(userId, "chapter-num", ' ')
        }).Trim();
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
        UpdateRelationForKind(dto.Annuals, series.Relations.Where(r => r.RelationKind == RelationKind.Annual).ToList(), series, RelationKind.Annual);

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

            series.Relations.Add(new SeriesRelation
            {
                Series = series,
                SeriesId = series.Id,
                TargetSeriesId = targetSeriesId,
                RelationKind = kind
            });
            _unitOfWork.SeriesRepository.Update(series);
        }
    }

    public async Task<NextExpectedChapterDto> GetEstimatedChapterCreationDate(int seriesId, int userId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));
        if (!(await _unitOfWork.UserRepository.HasAccessToSeries(userId, seriesId)))
        {
            throw new UnauthorizedAccessException("user-no-access-library-from-series");
        }
        if (series.Metadata.PublicationStatus is not (PublicationStatus.OnGoing or PublicationStatus.Ended) ||
            (series.Library.Type is LibraryType.Book or LibraryType.LightNovel))
        {
            return _emptyExpectedChapter;
        }

        const int minimumTimeDeltas = 3;
        var chapters = _unitOfWork.ChapterRepository.GetChaptersForSeries(seriesId)
            .Where(c => !c.IsSpecial)
            .OrderBy(c => c.CreatedUtc)
            .ToList();

        if (chapters.Count < 3) return _emptyExpectedChapter;

        // Calculate the time differences between consecutive chapters
        var timeDifferences = new List<TimeSpan>();
        DateTime? previousChapterTime = null;
        foreach (var chapter in chapters)
        {
            if (previousChapterTime.HasValue && (chapter.CreatedUtc - previousChapterTime.Value) <= TimeSpan.FromHours(1))
            {
                continue; // Skip this chapter if it's within an hour of the previous one
            }

            if ((chapter.CreatedUtc - previousChapterTime ?? TimeSpan.Zero) != TimeSpan.Zero)
            {
                timeDifferences.Add(chapter.CreatedUtc - previousChapterTime ?? TimeSpan.Zero);
            }

            previousChapterTime = chapter.CreatedUtc;
        }

        if (timeDifferences.Count < minimumTimeDeltas)
        {
            return _emptyExpectedChapter;
        }

        var historicalTimeDifferences = timeDifferences.Select(td => td.TotalDays).ToList();

        if (historicalTimeDifferences.Count < minimumTimeDeltas)
        {
            return _emptyExpectedChapter;
        }

        const double alpha = 0.2; // A smaller alpha will give more weight to recent data, while a larger alpha will smooth the data more.
        var forecastedTimeDifference = ExponentialSmoothing(historicalTimeDifferences, alpha);

        if (forecastedTimeDifference <= 0)
        {
            return _emptyExpectedChapter;
        }

        // Calculate the forecast for when the next chapter is expected
        var nextChapterExpected = chapters.Any()
            ? chapters.Max(c => c.CreatedUtc) + TimeSpan.FromDays(forecastedTimeDifference)
            : (DateTime?)null;

        // For number and volume number, we need the highest chapter, not the latest created
        var lastChapter = chapters.MaxBy(c => c.MaxNumber)!;
        var lastChapterNumber = lastChapter.MaxNumber;

        var lastVolumeNum = chapters.Select(c => c.Volume.MinNumber).Max();

        var result = new NextExpectedChapterDto
        {
            ChapterNumber = 0,
            VolumeNumber = Parser.LooseLeafVolumeNumber,
            ExpectedDate = nextChapterExpected,
            Title = string.Empty
        };

        if (lastChapterNumber > 0)
        {
            result.ChapterNumber = (int) Math.Truncate(lastChapterNumber) + 1;
            result.VolumeNumber = lastChapter.Volume.MinNumber;
            result.Title = series.Library.Type switch
            {
                LibraryType.Manga => await _localizationService.Translate(userId, "chapter-num", result.ChapterNumber),
                LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", "#", result.ChapterNumber),
                LibraryType.Book => await _localizationService.Translate(userId, "book-num", result.ChapterNumber),
                LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", result.ChapterNumber),
                _ => await _localizationService.Translate(userId, "chapter-num", result.ChapterNumber)
            };
        }
        else
        {
            result.VolumeNumber = lastVolumeNum + 1;
            result.Title = await _localizationService.Translate(userId, "volume-num", result.VolumeNumber);
        }


        return result;
    }

    private static double ExponentialSmoothing(IList<double> data, double alpha)
    {
        var forecast = data[0];

        foreach (var value in data)
        {
            forecast = alpha * value + (1 - alpha) * forecast;
        }

        return forecast;
    }
}
