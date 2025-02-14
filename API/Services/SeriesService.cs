using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
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

    Task<string> FormatChapterTitle(int userId, bool isSpecial, LibraryType libraryType, string chapterRange, string? chapterTitle,
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
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
            if (series == null) return false;

            series.Metadata ??= new SeriesMetadataBuilder()
                .Build();

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
            }
            else
            {
                if (!series.Metadata.AgeRatingLocked)
                {
                    var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
                    var allTags = series.Metadata.Tags.Select(t => t.Title).Concat(series.Metadata.Genres.Select(g => g.Title));
                    var updatedRating = ExternalMetadataService.DetermineAgeRating(allTags, metadataSettings.AgeRatingMappings);
                    if (updatedRating > series.Metadata.AgeRating)
                    {
                        series.Metadata.AgeRating = updatedRating;
                    }
                }
            }

            if (updateSeriesMetadataDto.SeriesMetadata != null)
            {
                if (PersonHelper.HasAnyPeople(updateSeriesMetadataDto.SeriesMetadata))
                {
                    series.Metadata.People ??= [];

                    // Writers
                    if (!series.Metadata.WriterLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Writers, PersonRole.Writer, _unitOfWork);
                    }

                    // Cover Artists
                    if (!series.Metadata.CoverArtistLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.CoverArtists, PersonRole.CoverArtist, _unitOfWork);
                    }

                    // Colorists
                    if (!series.Metadata.ColoristLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Colorists, PersonRole.Colorist, _unitOfWork);
                    }

                    // Editors
                    if (!series.Metadata.EditorLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Editors, PersonRole.Editor, _unitOfWork);
                    }

                    // Inkers
                    if (!series.Metadata.InkerLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Inkers, PersonRole.Inker, _unitOfWork);
                    }

                    // Letterers
                    if (!series.Metadata.LettererLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Letterers, PersonRole.Letterer, _unitOfWork);
                    }

                    // Pencillers
                    if (!series.Metadata.PencillerLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Pencillers, PersonRole.Penciller, _unitOfWork);
                    }

                    // Publishers
                    if (!series.Metadata.PublisherLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Publishers, PersonRole.Publisher, _unitOfWork);
                    }

                    // Imprints
                    if (!series.Metadata.ImprintLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Imprints, PersonRole.Imprint, _unitOfWork);
                    }

                    // Teams
                    if (!series.Metadata.TeamLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Teams, PersonRole.Team, _unitOfWork);
                    }

                    // Locations
                    if (!series.Metadata.LocationLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Locations, PersonRole.Location, _unitOfWork);
                    }

                    // Translators
                    if (!series.Metadata.TranslatorLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Translators, PersonRole.Translator, _unitOfWork);
                    }

                    // Characters
                    if (!series.Metadata.CharacterLocked)
                    {
                        await HandlePeopleUpdateAsync(series.Metadata, updateSeriesMetadataDto.SeriesMetadata.Characters, PersonRole.Character, _unitOfWork);
                    }

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
        var existingPeopleDictionary = existingPeople.DistinctBy(p => p.NormalizedName)
            .ToDictionary(p => p.NormalizedName, p => p);

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

            // NOTE: This isn't getting all the people and whatnot currently
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
            chapter.Title = await FormatChapterTitle(userId, chapter, libraryType);

            if (!chapter.IsSpecial) continue;
            specials.Add(chapter);
        }

        // Don't show chapter -100000 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
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

    /// <summary>
    /// Should the volume be included and if so, this renames
    /// </summary>
    /// <param name="volume"></param>
    /// <param name="libraryType"></param>
    /// <param name="volumeLabel"></param>
    /// <returns></returns>
    public static bool RenameVolumeName(VolumeDto volume, LibraryType libraryType, string volumeLabel = "Volume")
    {
        if (libraryType is LibraryType.Book or LibraryType.LightNovel)
        {
            var firstChapter = volume.Chapters.First();
            // On Books, skip volumes that are specials, since these will be shown
            // if (firstChapter.IsSpecial)
            // {
            //     // Some books can be SP marker and also position of 0, this will trick Kavita into rendering it as part of a non-special volume
            //     // We need to rename the entity so that it renders out correctly
            //     return false;
            // }
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

            return !firstChapter.IsSpecial;
        }

        volume.Name = $"{volumeLabel.Trim()} {volume.Name}".Trim();
        return true;
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
        // var nextChapterExpected = chapters.Count > 0
        //     ? chapters.Max(c => c.CreatedUtc) + TimeSpan.FromDays(forecastedTimeDifference)
        //     : (DateTime?)null;
        var lastChapterDate = chapters.Max(c => c.CreatedUtc);
        var estimatedDate = lastChapterDate.AddDays(forecastedTimeDifference);
        var nextChapterExpected = estimatedDate.Day > DateTime.DaysInMonth(estimatedDate.Year, estimatedDate.Month)
            ? new DateTime(estimatedDate.Year, estimatedDate.Month, DateTime.DaysInMonth(estimatedDate.Year, estimatedDate.Month))
            : estimatedDate;

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
                LibraryType.ComicVine => await _localizationService.Translate(userId, "issue-num", "#", result.ChapterNumber),
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
