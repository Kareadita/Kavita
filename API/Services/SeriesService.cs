using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.Person;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.MetadataMatching;
using API.Entities.Person;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Helpers.Formatting;
using API.Services.Plus;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds);
    Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId);
    [Obsolete("Use LocalizedNamingContext")]
    Task<string> FormatChapterTitle(int userId, ChapterDto chapter, LibraryType libraryType, bool withHash = true);
    [Obsolete("Use LocalizedNamingContext")]
    Task<string> FormatChapterTitle(int userId, Chapter chapter, LibraryType libraryType, bool withHash = true);
    [Obsolete("Use LocalizedNamingContext")]
    Task<string> FormatChapterTitle(int userId, bool isSpecial, LibraryType libraryType, string chapterRange, string? chapterTitle,
        bool withHash);
    [Obsolete("Use LocalizedNamingContext")]
    Task<string> FormatChapterName(int userId, LibraryType libraryType, bool withHash = false);
    Task<NextExpectedChapterDto> GetEstimatedChapterCreationDate(int seriesId, int userId);
    Task<PagedList<SeriesDto>> GetCurrentlyReading(int userId, int requestingUserId, UserParams userParams);
    Task<List<FilterStatementDto>> GetProfilePrivacyStatements(int userId, int requestingUserId);
}

public class SeriesService : ISeriesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<SeriesService> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly IReadingListService _readingListService;
    private readonly IEntityNamingService _namingService;

    private readonly NextExpectedChapterDto _emptyExpectedChapter = new NextExpectedChapterDto
    {
        ExpectedDate = null,
        ChapterNumber = 0,
        VolumeNumber = Parser.LooseLeafVolumeNumber
    };

    public SeriesService(IUnitOfWork unitOfWork, IEventHub eventHub, ITaskScheduler taskScheduler,
        ILogger<SeriesService> logger, ILocalizationService localizationService, IReadingListService readingListService,
        IEntityNamingService namingService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _taskScheduler = taskScheduler;
        _logger = logger;
        _localizationService = localizationService;
        _readingListService = readingListService;
        _namingService = namingService;
    }

    /// <summary>
    /// Returns the first chapter for a series to extract metadata from (ie Summary, etc.)
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
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
            if (series == null) return false;

            series.Metadata ??= new SeriesMetadataBuilder()
                .Build();

            if (NumberHelper.IsValidYear(updateSeriesMetadataDto.SeriesMetadata.ReleaseYear) && series.Metadata.ReleaseYear != updateSeriesMetadataDto.SeriesMetadata.ReleaseYear)
            {
                series.Metadata.ReleaseYear = updateSeriesMetadataDto.SeriesMetadata.ReleaseYear;
                series.Metadata.ReleaseYearLocked = true;
                series.Metadata.KPlusOverrides.Remove(MetadataSettingField.StartDate);
            }

            if (series.Metadata.PublicationStatus != updateSeriesMetadataDto.SeriesMetadata.PublicationStatus)
            {
                series.Metadata.PublicationStatus = updateSeriesMetadataDto.SeriesMetadata.PublicationStatus;
                series.Metadata.PublicationStatusLocked = true;
                series.Metadata.KPlusOverrides.Remove(MetadataSettingField.PublicationStatus);
            }

            if (string.IsNullOrEmpty(updateSeriesMetadataDto.SeriesMetadata.Summary))
            {
                updateSeriesMetadataDto.SeriesMetadata.Summary = string.Empty;
                series.Metadata.KPlusOverrides.Remove(MetadataSettingField.Summary);
            }

            if (series.Metadata.Summary != updateSeriesMetadataDto.SeriesMetadata.Summary.Trim())
            {
                series.Metadata.Summary = updateSeriesMetadataDto.SeriesMetadata?.Summary.Trim() ?? string.Empty;
                series.Metadata.SummaryLocked = true;
                series.Metadata.KPlusOverrides.Remove(MetadataSettingField.Summary);
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
                series.Metadata.WebLinks = string.Join(',', updateSeriesMetadataDto.SeriesMetadata?.WebLinks
                    .Split(',')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Trim())!
                );
            }


            if (updateSeriesMetadataDto.SeriesMetadata?.Genres != null &&
                updateSeriesMetadataDto.SeriesMetadata.Genres.Count != 0)
            {
                var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(updateSeriesMetadataDto.SeriesMetadata.Genres.Select(t => Parser.Normalize(t.Title)))).ToList();
                series.Metadata.Genres ??= [];
                GenreHelper.UpdateGenreList(updateSeriesMetadataDto.SeriesMetadata?.Genres, series, allGenres, genre =>
                {
                    series.Metadata.Genres.Add(genre);
                }, () => series.Metadata.GenresLocked = true);
            }
            else
            {
                series.Metadata.Genres = [];
            }


            if (updateSeriesMetadataDto.SeriesMetadata?.Tags is {Count: > 0})
            {
                var allTags = (await _unitOfWork.TagRepository
                    .GetAllTagsByNameAsync(updateSeriesMetadataDto.SeriesMetadata.Tags.Select(t => Parser.Normalize(t.Title))))
                    .ToList();
                series.Metadata.Tags ??= [];
                TagHelper.UpdateTagList(updateSeriesMetadataDto.SeriesMetadata?.Tags, series, allTags, tag =>
                {
                    series.Metadata.Tags.Add(tag);
                }, () => series.Metadata.TagsLocked = true);
            }
            else
            {
                series.Metadata.Tags = [];
            }

            if (series.Metadata.AgeRating != updateSeriesMetadataDto.SeriesMetadata?.AgeRating)
            {
                series.Metadata.AgeRating = updateSeriesMetadataDto.SeriesMetadata?.AgeRating ?? AgeRating.Unknown;
                series.Metadata.AgeRatingLocked = true;
                await _readingListService.UpdateReadingListAgeRatingForSeries(series.Id, series.Metadata.AgeRating);
                series.Metadata.KPlusOverrides.Remove(MetadataSettingField.AgeRating);
            }
            else
            {
                if (!series.Metadata.AgeRatingLocked)
                {
                    var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
                    var allTags = series.Metadata.Tags.Select(t => t.Title).Concat(series.Metadata.Genres.Select(g => g.Title));

                    if (metadataSettings.EnableExtendedMetadataProcessing)
                    {
                        var updatedRating = ExternalMetadataService.DetermineAgeRating(allTags, metadataSettings.AgeRatingMappings);
                        if (updatedRating > series.Metadata.AgeRating)
                        {
                            series.Metadata.AgeRating = updatedRating;
                            series.Metadata.KPlusOverrides.Remove(MetadataSettingField.AgeRating);
                        }
                    }

                }
            }

            // Update people and locks
            if (updateSeriesMetadataDto.SeriesMetadata != null)
            {
                series.Metadata.People ??= [];

                // Writers
                if (!series.Metadata.WriterLocked || !updateSeriesMetadataDto.SeriesMetadata.WriterLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Writers, PersonRole.Writer, _unitOfWork);
                }

                // Cover Artists
                if (!series.Metadata.CoverArtistLocked || !updateSeriesMetadataDto.SeriesMetadata.CoverArtistLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.CoverArtists, PersonRole.CoverArtist, _unitOfWork);
                }

                // Colorists
                if (!series.Metadata.ColoristLocked || !updateSeriesMetadataDto.SeriesMetadata.ColoristLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Colorists, PersonRole.Colorist, _unitOfWork);
                }

                // Editors
                if (!series.Metadata.EditorLocked || !updateSeriesMetadataDto.SeriesMetadata.EditorLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Editors, PersonRole.Editor, _unitOfWork);
                }

                // Inkers
                if (!series.Metadata.InkerLocked || !updateSeriesMetadataDto.SeriesMetadata.InkerLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Inkers, PersonRole.Inker, _unitOfWork);
                }

                // Letterers
                if (!series.Metadata.LettererLocked || !updateSeriesMetadataDto.SeriesMetadata.LettererLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Letterers, PersonRole.Letterer, _unitOfWork);
                }

                // Pencillers
                if (!series.Metadata.PencillerLocked || !updateSeriesMetadataDto.SeriesMetadata.PencillerLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Pencillers, PersonRole.Penciller, _unitOfWork);
                }

                // Publishers
                if (!series.Metadata.PublisherLocked || !updateSeriesMetadataDto.SeriesMetadata.PublisherLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Publishers, PersonRole.Publisher, _unitOfWork);
                }

                // Imprints
                if (!series.Metadata.ImprintLocked || !updateSeriesMetadataDto.SeriesMetadata.ImprintLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Imprints, PersonRole.Imprint, _unitOfWork);
                }

                // Teams
                if (!series.Metadata.TeamLocked || !updateSeriesMetadataDto.SeriesMetadata.TeamLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Teams, PersonRole.Team, _unitOfWork);
                }

                // Locations
                if (!series.Metadata.LocationLocked || !updateSeriesMetadataDto.SeriesMetadata.LocationLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Locations, PersonRole.Location, _unitOfWork);
                }

                // Translators
                if (!series.Metadata.TranslatorLocked || !updateSeriesMetadataDto.SeriesMetadata.TranslatorLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Translators, PersonRole.Translator, _unitOfWork);
                }

                // Characters
                if (!series.Metadata.CharacterLocked || !updateSeriesMetadataDto.SeriesMetadata.CharacterLocked)
                {
                    await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Characters, PersonRole.Character, _unitOfWork);
                }

                series.Metadata.AgeRatingLocked = updateSeriesMetadataDto.SeriesMetadata.AgeRatingLocked;
                series.Metadata.PublicationStatusLocked = updateSeriesMetadataDto.SeriesMetadata.PublicationStatusLocked;
                series.Metadata.LanguageLocked = updateSeriesMetadataDto.SeriesMetadata.LanguageLocked;
                series.Metadata.GenresLocked = updateSeriesMetadataDto.SeriesMetadata.GenresLocked;
                series.Metadata.TagsLocked = updateSeriesMetadataDto.SeriesMetadata.TagsLocked;
                series.Metadata.CharacterLocked = updateSeriesMetadataDto.SeriesMetadata.CharacterLocked;
                series.Metadata.ColoristLocked = updateSeriesMetadataDto.SeriesMetadata.ColoristLocked;
                series.Metadata.EditorLocked = updateSeriesMetadataDto.SeriesMetadata.EditorLocked;
                series.Metadata.InkerLocked = updateSeriesMetadataDto.SeriesMetadata.InkerLocked;
                series.Metadata.ImprintLocked = updateSeriesMetadataDto.SeriesMetadata.ImprintLocked;
                series.Metadata.LettererLocked = updateSeriesMetadataDto.SeriesMetadata.LettererLocked;
                series.Metadata.PencillerLocked = updateSeriesMetadataDto.SeriesMetadata.PencillerLocked;
                series.Metadata.PublisherLocked = updateSeriesMetadataDto.SeriesMetadata.PublisherLocked;
                series.Metadata.TranslatorLocked = updateSeriesMetadataDto.SeriesMetadata.TranslatorLocked;
                series.Metadata.LocationLocked = updateSeriesMetadataDto.SeriesMetadata.LocationLocked;
                series.Metadata.CoverArtistLocked = updateSeriesMetadataDto.SeriesMetadata.CoverArtistLocked;
                series.Metadata.WriterLocked = updateSeriesMetadataDto.SeriesMetadata.WriterLocked;
                series.Metadata.SummaryLocked = updateSeriesMetadataDto.SeriesMetadata.SummaryLocked;
                series.Metadata.ReleaseYearLocked = updateSeriesMetadataDto.SeriesMetadata.ReleaseYearLocked;
            }

            if (!_unitOfWork.HasChanges())
            {
                return true;
            }

            _unitOfWork.SeriesRepository.Update(series.Metadata);
            await _unitOfWork.CommitAsync();

            // Trigger code to clean up tags, collections, people, etc
            try
            {
                await _taskScheduler.CleanupDbEntries();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue cleaning up DB entries. This may happen if Komf is spamming updates. Nightly cleanup will work");
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

    /// <summary>
    /// Exclusively for Series Update API
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="peopleDtos"></param>
    /// <param name="role"></param>
    public static async Task HandlePeopleUpdateAsync(SeriesMetadata metadata, ICollection<PersonDto> peopleDtos, PersonRole role, IUnitOfWork unitOfWork)
    {
        // TODO: Cleanup this code so we aren't using UnitOfWork like this

        // Normalize all names from the DTOs
        var normalizedNames = peopleDtos
            .Select(p => Parser.Normalize(p.Name))
            .Distinct()
            .ToList();

        // Bulk select people who already exist in the database
        var existingPeople = await unitOfWork.PersonRepository.GetPeopleByNames(normalizedNames);

        // Use a dictionary for quick lookups
        var existingPeopleDictionary = PersonHelper.ConstructNameAndAliasDictionary(existingPeople);

        // List to track people that will be added to the metadata
        var peopleToAdd = new List<Person>();

        foreach (var personDto in peopleDtos)
        {
            var normalizedPersonName = Parser.Normalize(personDto.Name);

            // Check if the person exists in the dictionary
            if (existingPeopleDictionary.TryGetValue(normalizedPersonName, out var p))
            {
                // TODO: Should I add more controls here to map back?
                if (personDto.AniListId > 0 && p.AniListId <= 0 && p.AniListId != personDto.AniListId)
                {
                    p.AniListId = personDto.AniListId;
                }
                p.Description = string.IsNullOrEmpty(p.Description) ? personDto.Description : p.Description;
                continue; // If we ever want to update metadata for existing people, we'd do it here
            }

            // Person doesn't exist, so create a new one
            var newPerson = new Person
            {
                Name = personDto.Name,
                NormalizedName = normalizedPersonName,
                AniListId = personDto.AniListId,
                Description = personDto.Description,
                Asin = personDto.Asin,
                CoverImage = personDto.CoverImage,
                MalId =  personDto.MalId,
                HardcoverId = personDto.HardcoverId,
            };

            peopleToAdd.Add(newPerson);
            existingPeopleDictionary[normalizedPersonName] = newPerson;
        }

        // Add any new people to the database in bulk
        if (peopleToAdd.Count != 0)
        {
            unitOfWork.PersonRepository.Attach(peopleToAdd);
        }

        // Now that we have all the people (new and existing), update the SeriesMetadataPeople
        UpdateSeriesMetadataPeople(metadata, metadata.People, existingPeopleDictionary.Values, role);
    }

    private static void UpdateSeriesMetadataPeople(SeriesMetadata metadata, ICollection<SeriesMetadataPeople> metadataPeople, IEnumerable<Person> people, PersonRole role)
    {
        var peopleToAdd = people.ToList();

        // Remove any people in the existing metadataPeople for this role that are no longer present in the input list
        var peopleToRemove = metadataPeople
            .Where(mp => mp.Role == role && peopleToAdd.TrueForAll(p => p.NormalizedName != mp.Person.NormalizedName))
            .ToList();

        foreach (var personToRemove in peopleToRemove)
        {
            metadataPeople.Remove(personToRemove);
        }

        // Add new people for this role if they don't already exist
        foreach (var person in peopleToAdd)
        {
            var existingPersonEntry = metadataPeople
                .FirstOrDefault(mp => mp.Person.NormalizedName == person.NormalizedName && mp.Role == role);

            if (existingPersonEntry == null)
            {
                metadataPeople.Add(new SeriesMetadataPeople
                {
                    PersonId = person.Id,
                    Person = person,
                    SeriesMetadataId = metadata.Id,
                    SeriesMetadata = metadata,
                    Role = role
                });
            }
        }
    }


    public async Task<bool> DeleteMultipleSeries(IList<int> seriesIds)
    {
        try
        {
            var chapterMappings =
                await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync([.. seriesIds]);

            var allChapterIds = new List<int>();
            foreach (var mapping in chapterMappings)
            {
                allChapterIds.AddRange(mapping.Value);
            }

            // NOTE: This isn't getting all the people and whatnot currently due to the lack of includes
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
            await _unitOfWork.CollectionTagRepository.RemoveCollectionsWithoutSeries();
            _taskScheduler.CleanupChapters([.. allChapterIds]);

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

        var libraryIds = await _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);
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
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId);
        var namingContext = await LocalizedNamingContext.CreateAsync( _namingService, _localizationService, userId, libraryType);
        var bookTreatment = libraryType is LibraryType.Book or LibraryType.LightNovel;

        // For books, the Name of the Volume is remapped to the actual name of the book, rather than Volume number.
        var processedVolumes = new List<VolumeDto>();
        foreach (var volume in volumes)
        {
            if (volume.IsLooseLeaf() || volume.IsSpecial())
            {
                continue;
            }

            var formattedName = namingContext.FormatVolumeName(volume);
            if (formattedName != null)
            {
                volume.Name = formattedName;
                processedVolumes.Add(volume);
            }
            else if (bookTreatment && !volume.IsSpecial())
            {
                // Edge case: FormatVolumeName returned null but book treatment wants it
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
            chapter.Title = namingContext.FormatChapterTitle(chapter);

            if (!chapter.IsSpecial) continue;
            specials.Add(chapter);
        }

        // Don't show chapter -100000 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
        IEnumerable<ChapterDto> retChapters = bookTreatment ? [] : chapters.Where(ShouldIncludeChapter);

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
            // TODO: See if we can get the ContinueFrom here
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


    public async Task<string> FormatChapterTitle(int userId, bool isSpecial, LibraryType libraryType, string chapterRange, string? chapterTitle, bool withHash)
    {
        if (string.IsNullOrEmpty(chapterTitle) && (isSpecial || libraryType == LibraryType.Book)) throw new ArgumentException("Chapter Title cannot be null");

        if (isSpecial)
        {
            return Parser.CleanSpecialTitle(chapterTitle!);
        }

        var hashSpot = withHash ? "#" : string.Empty;
        var baseChapter = libraryType switch
        {
            LibraryType.Book => await _localizationService.Translate(userId, "book-num", chapterTitle!),
            LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", chapterRange),
            LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", hashSpot, chapterRange),
            LibraryType.ComicVine => await _localizationService.Translate(userId, "issue-num", hashSpot, chapterRange),
            LibraryType.Manga => await _localizationService.Translate(userId, "chapter-num", chapterRange),
            LibraryType.Image => await _localizationService.Translate(userId, "chapter-num", chapterRange),
            _ => await _localizationService.Translate(userId, "chapter-num", ' ')
        };

        if (!string.IsNullOrEmpty(chapterTitle) && libraryType != LibraryType.Book && chapterTitle != chapterRange)
        {
            baseChapter += " - " + chapterTitle;
        }


        return baseChapter;
    }

    public async Task<string> FormatChapterTitle(int userId, ChapterDto chapter, LibraryType libraryType, bool withHash = true)
    {
        return await FormatChapterTitle(userId, chapter.IsSpecial, libraryType, chapter.Range, chapter.Title, withHash);
    }

    public async Task<string> FormatChapterTitle(int userId, Chapter chapter, LibraryType libraryType, bool withHash = true)
    {
        return await FormatChapterTitle(userId, chapter.IsSpecial, libraryType, chapter.Range, chapter.Title, withHash);
    }

    // TODO: Refactor this out and use FormatChapterTitle instead across library
    public async Task<string> FormatChapterName(int userId, LibraryType libraryType, bool withHash = false)
    {
        var hashSpot = withHash ? "#" : string.Empty;
        return (libraryType switch
        {
            LibraryType.Book => await _localizationService.Translate(userId, "book-num", string.Empty),
            LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", string.Empty),
            LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", hashSpot, string.Empty),
            LibraryType.ComicVine => await _localizationService.Translate(userId, "issue-num", hashSpot, string.Empty),
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
    /// Update the relations attached to the Series. Generates associated Sequel/Prequel pairs on target series.
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
        UpdateRelationForKind(dto.Editions, series.Relations.Where(r => r.RelationKind == RelationKind.Edition).ToList(), series, RelationKind.Edition);
        UpdateRelationForKind(dto.Annuals, series.Relations.Where(r => r.RelationKind == RelationKind.Annual).ToList(), series, RelationKind.Annual);

        await UpdatePrequelSequelRelations(dto.Prequels, series, RelationKind.Prequel);
        await UpdatePrequelSequelRelations(dto.Sequels, series, RelationKind.Sequel);

        if (!_unitOfWork.HasChanges()) return true;
        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Updates Prequel/Sequel relations and creates reciprocal relations on target series.
    /// </summary>
    /// <param name="targetSeriesIds">List of target series IDs</param>
    /// <param name="series">The current series being updated</param>
    /// <param name="kind">The relation kind (Prequel or Sequel)</param>
    private async Task UpdatePrequelSequelRelations(ICollection<int> targetSeriesIds, Series series, RelationKind kind)
    {
        var existingRelations = series.Relations.Where(r => r.RelationKind == kind).ToList();

        // Remove relations that are not in the new list
        foreach (var relation in existingRelations.Where(relation => !targetSeriesIds.Contains(relation.TargetSeriesId)))
        {
            series.Relations.Remove(relation);
            await RemoveReciprocalRelation(series.Id, relation.TargetSeriesId, GetOppositeRelationKind(kind));
        }

        // Add new relations
        foreach (var targetSeriesId in targetSeriesIds)
        {
            if (series.Relations.Any(r => r.RelationKind == kind && r.TargetSeriesId == targetSeriesId))
                continue;

            series.Relations.Add(new SeriesRelation
            {
                Series = series,
                SeriesId = series.Id,
                TargetSeriesId = targetSeriesId,
                RelationKind = kind
            });

            await AddReciprocalRelation(series.Id, targetSeriesId, GetOppositeRelationKind(kind));
        }

        _unitOfWork.SeriesRepository.Update(series);
    }

    private static RelationKind GetOppositeRelationKind(RelationKind kind)
    {
        return kind == RelationKind.Prequel ? RelationKind.Sequel : RelationKind.Prequel;
    }

    private async Task AddReciprocalRelation(int sourceSeriesId, int targetSeriesId, RelationKind kind)
    {
        var targetSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(targetSeriesId, SeriesIncludes.Related);
        if (targetSeries == null) return;

        if (targetSeries.Relations.Any(r => r.RelationKind == kind && r.TargetSeriesId == sourceSeriesId))
            return;

        targetSeries.Relations.Add(new SeriesRelation
        {
            Series = targetSeries,
            SeriesId = targetSeriesId,
            TargetSeriesId = sourceSeriesId,
            RelationKind = kind
        });

        _unitOfWork.SeriesRepository.Update(targetSeries);
    }

    private async Task RemoveReciprocalRelation(int sourceSeriesId, int targetSeriesId, RelationKind kind)
    {
        var targetSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(targetSeriesId, SeriesIncludes.Related);
        if (targetSeries == null) return;

        var relationToRemove = targetSeries.Relations.FirstOrDefault(r => r.RelationKind == kind && r.TargetSeriesId == sourceSeriesId);
        if (relationToRemove != null)
        {
            targetSeries.Relations.Remove(relationToRemove);
            _unitOfWork.SeriesRepository.Update(targetSeries);
        }
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

        // Estimation only makes sense for ongoing/ended manga/comics - books and light novels
        // don't follow predictable release patterns based on chapter creation dates
        if (series.Metadata.PublicationStatus is not (PublicationStatus.OnGoing or PublicationStatus.Ended) ||
            (series.Library.Type is LibraryType.Book or LibraryType.LightNovel))
        {
            return _emptyExpectedChapter;
        }

        // We need at least 3 chapters to establish a meaningful pattern for prediction.
        // With fewer data points, exponential smoothing produces unreliable forecasts.
        const int minimumChaptersRequired = 3;
        const int minimumTimeDeltas = 3;

        // Only fetch the fields we need for calculation - avoids loading entire Chapter entities
        // with all their navigation properties, significantly reducing memory and query time
        var chapterData = await _unitOfWork.ChapterRepository.GetChaptersForSeries(seriesId)
            .Where(c => !c.IsSpecial)
            .Select(c => new
            {
                c.CreatedUtc,
                c.MaxNumber,
                VolumeMinNumber = c.Volume.MinNumber
            })
            .OrderBy(c => c.CreatedUtc)
            .ToListAsync();

        if (chapterData.Count < minimumChaptersRequired) return _emptyExpectedChapter;

        // Pre-allocate with maximum possible capacity to avoid list resizing during iteration.
        // We store as days (double) directly to avoid a second conversion pass later.
        var timeDifferencesInDays = new List<double>(chapterData.Count - 1);

        // These track the values we need for building the result DTO.
        // Previously we iterated the list 4 separate times with LINQ - now we gather everything in one pass.
        var lastChapterDate = DateTime.MinValue;
        var highestChapterNumber = float.MinValue;
        var highestChapterIndex = 0;
        var highestVolumeNumber = float.MinValue;

        DateTime? previousChapterTime = null;

        for (var i = 0; i < chapterData.Count; i++)
        {
            var chapter = chapterData[i];

            // Find the most recently created chapter - used as the baseline for our prediction.
            // "When was the last chapter added?" + forecasted interval = expected next chapter date
            if (chapter.CreatedUtc > lastChapterDate)
            {
                lastChapterDate = chapter.CreatedUtc;
            }

            // Find the chapter with the highest number - this determines what number comes next.
            // Note: This is different from "most recent" - a user might add Chapter 50 before Chapter 49
            if (chapter.MaxNumber > highestChapterNumber)
            {
                highestChapterNumber = chapter.MaxNumber;
                highestChapterIndex = i;
            }

            // Track the highest volume number for series that use volume-based numbering
            // (e.g., "Volume 5" instead of "Chapter 47")
            if (chapter.VolumeMinNumber > highestVolumeNumber)
            {
                highestVolumeNumber = chapter.VolumeMinNumber;
            }

            // Build the time differences array for exponential smoothing.
            // We skip chapters added within 1 hour of each other - these are typically bulk imports
            // or batch uploads that don't represent the actual release cadence.
            if (previousChapterTime.HasValue)
            {
                var daysBetweenChapters = (chapter.CreatedUtc - previousChapterTime.Value).TotalDays;
                var hoursBetweenChapters = daysBetweenChapters * 24;

                if (hoursBetweenChapters > 1)
                {
                    timeDifferencesInDays.Add(daysBetweenChapters);
                    previousChapterTime = chapter.CreatedUtc;
                }
                // If within an hour, we intentionally don't update previousChapterTime
                // so the next valid chapter measures from the last "real" release
            }
            else
            {
                previousChapterTime = chapter.CreatedUtc;
            }
        }

        // After filtering out bulk imports, we may not have enough data points for a reliable forecast
        if (timeDifferencesInDays.Count < minimumTimeDeltas)
        {
            return _emptyExpectedChapter;
        }

        // Exponential smoothing weights recent releases more heavily than older ones.
        // Alpha of 0.2 means ~80% weight on historical average, ~20% on recent data.
        // This smooths out anomalies like holiday delays or double-releases.
        const double alpha = 0.2;
        var forecastedDaysBetweenChapters = ExponentialSmoothing(timeDifferencesInDays, alpha);

        if (forecastedDaysBetweenChapters <= 0)
        {
            return _emptyExpectedChapter;
        }

        // Calculate expected date by adding the forecasted interval to the last chapter's creation date
        var estimatedDate = lastChapterDate.AddDays(forecastedDaysBetweenChapters);

        // Clamp to valid calendar date - AddDays can produce invalid dates like "February 30th"
        // when the forecasted date would land past the end of a short month
        var nextChapterExpected = estimatedDate.Day > DateTime.DaysInMonth(estimatedDate.Year, estimatedDate.Month)
            ? new DateTime(estimatedDate.Year, estimatedDate.Month, DateTime.DaysInMonth(estimatedDate.Year, estimatedDate.Month))
            : estimatedDate;

        // Build the result with the next expected chapter/volume number
        var result = new NextExpectedChapterDto
        {
            ChapterNumber = 0,
            VolumeNumber = Parser.LooseLeafVolumeNumber,
            ExpectedDate = nextChapterExpected,
            Title = string.Empty
        };

        // Series can be numbered by chapter (most manga/comics) or by volume (some collected editions).
        // If we have chapter numbers, increment from the highest; otherwise, increment the volume number.
        if (highestChapterNumber > 0)
        {
            result.ChapterNumber = (int)Math.Truncate(highestChapterNumber) + 1;
            result.VolumeNumber = chapterData[highestChapterIndex].VolumeMinNumber;

            // Format the title based on library type conventions
            // Manga uses "Chapter X", Comics use "Issue #X", Books use "Book X"
            result.Title = series.Library.Type switch
            {
                LibraryType.Manga => await _localizationService.Translate(userId, "chapter-num", result.ChapterNumber),
                LibraryType.Comic => await _localizationService.Translate(userId, "issue-num", "#", result.ChapterNumber),
                LibraryType.ComicVine => await _localizationService.Translate(userId, "issue-num", "#", result.ChapterNumber),
                LibraryType.Book => await _localizationService.Translate(userId, "book-num", result.ChapterNumber),
                LibraryType.LightNovel => await _localizationService.Translate(userId, "book-num", result.ChapterNumber),
                _ => await _localizationService.Translate(userId, "chapter-num", result.ChapterNumber)
            };
        }
        else
        {
            // Volume-only numbering - common for omnibus editions or series without chapter breaks
            result.VolumeNumber = (int)highestVolumeNumber + 1;
            result.Title = await _localizationService.Translate(userId, "volume-num", result.VolumeNumber);
        }

        return result;
    }

    public async Task<PagedList<SeriesDto>> GetCurrentlyReading(int userId, int requestingUserId, UserParams userParams)
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var filter = new FilterV2Dto
        {
            Combination = FilterCombination.And,
            SortOptions = new SortOptions
            {
                SortField = SortField.ReadProgress,
                IsAscending = false,
            },
            Statements = [
                new FilterStatementDto
                {
                  Comparison = FilterComparison.GreaterThan,
                  Field = FilterField.ReadLast,
                  Value = serverSettings.OnDeckProgressDays.ToString(),
                },
                new FilterStatementDto
                {
                    Comparison = FilterComparison.LessThan,
                    Field = FilterField.ReadProgress,
                    Value = "100",
                },
                new FilterStatementDto
                {
                    Comparison = FilterComparison.GreaterThan,
                    Field = FilterField.ReadProgress,
                    Value = "0",
                },
            ],
        };

        filter.Statements.AddRange(await GetProfilePrivacyStatements(userId, requestingUserId));

        return await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, userParams, filter);
    }

    public async Task<List<FilterStatementDto>> GetProfilePrivacyStatements(int userId, int requestingUserId)
    {
        if (userId == requestingUserId) return [];

        var socialPreferences = await _unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = (await _unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId))!;

        var librariesUser = await _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);
        var librariesRequestingUser = await _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(requestingUserId);

        var libIds = librariesRequestingUser.Intersect(librariesUser);
        if (socialPreferences.SocialLibraries.Count > 0)
        {
            libIds = libIds.Intersect(socialPreferences.SocialLibraries);
        }

        var libraries = libIds.Select(id => id.ToString());

        var ageRating = socialPreferences.SocialMaxAgeRating < requestingUser.AgeRestriction ? socialPreferences.SocialMaxAgeRating : requestingUser.AgeRestriction;
        var includeUnknowns = socialPreferences.SocialIncludeUnknowns && requestingUser.AgeRestrictionIncludeUnknowns;

        List<FilterStatementDto> filters =
        [
            new()
            {
                Comparison = FilterComparison.Contains,
                Field = FilterField.Libraries,
                Value = string.Join(',', libraries),
            }

        ];

        if (!includeUnknowns)
        {
            filters.Add(new FilterStatementDto
            {
                Comparison = FilterComparison.NotEqual,
                Field = FilterField.AgeRating,
                Value = nameof(AgeRating.Unknown),
            });
        }

        if (ageRating != AgeRating.NotApplicable)
        {
            filters.Add(new FilterStatementDto
            {
                Comparison = FilterComparison.LessThanEqual,
                Field = FilterField.AgeRating,
                Value = ageRating.ToString(),
            });
        }

        return filters;
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
