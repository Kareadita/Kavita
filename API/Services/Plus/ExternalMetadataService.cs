using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Collection;
using API.DTOs.KavitaPlus.ExternalMetadata;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Metadata.Matching;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using AutoMapper;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable



public interface IExternalMetadataService
{
    Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId);
    Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType);
    Task FetchExternalDataTask();
    /// <summary>
    /// This is an entry point and provides a level of protection against calling upstream API. Will only allow 100 new
    /// series to fetch data within a day and enqueues background jobs at certain times to fetch that data.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <returns>If the fetch was made</returns>
    Task<bool> FetchSeriesMetadata(int seriesId, LibraryType libraryType);

    Task<IList<MalStackDto>> GetStacksForUser(int userId);
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto);
    Task FixSeriesMatch(int seriesId, int anilistId);
    Task UpdateSeriesDontMatch(int seriesId, bool dontMatch);
    Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId);
}

public class ExternalMetadataService : IExternalMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExternalMetadataService> _logger;
    private readonly IMapper _mapper;
    private readonly ILicenseService _licenseService;
    private readonly IScrobblingService _scrobblingService;
    private readonly IEventHub _eventHub;
    private readonly ICoverDbService _coverDbService;
    private readonly TimeSpan _externalSeriesMetadataCache = TimeSpan.FromDays(30);
    public static readonly HashSet<LibraryType> NonEligibleLibraryTypes =
        [LibraryType.Comic, LibraryType.Book, LibraryType.Image, LibraryType.ComicVine];
    private readonly SeriesDetailPlusDto _defaultReturn = new()
    {
        Recommendations = null,
        Ratings = ArraySegment<RatingDto>.Empty,
        Reviews = ArraySegment<UserReviewDto>.Empty
    };
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);
    static bool IsRomanCharacters(string input) => Regex.IsMatch(input, @"^[\p{IsBasicLatin}\p{IsLatin-1Supplement}]+$");

    public ExternalMetadataService(IUnitOfWork unitOfWork, ILogger<ExternalMetadataService> logger, IMapper mapper,
        ILicenseService licenseService, IScrobblingService scrobblingService, IEventHub eventHub, ICoverDbService coverDbService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _licenseService = licenseService;
        _scrobblingService = scrobblingService;
        _eventHub = eventHub;
        _coverDbService = coverDbService;

        FlurlConfiguration.ConfigureClientForUrl(Configuration.KavitaPlusApiUrl);
    }

    /// <summary>
    /// Checks if the library type is allowed to interact with Kavita+
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsPlusEligible(LibraryType type)
    {
        return !NonEligibleLibraryTypes.Contains(type);
    }

    /// <summary>
    /// This is a task that runs on a schedule and slowly fetches data from Kavita+ to keep
    /// data in the DB non-stale and fetched.
    /// </summary>
    /// <remarks>To avoid blasting Kavita+ API, this only processes 25 records. The goal is to slowly build out/refresh the data</remarks>
    /// <returns></returns>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task FetchExternalDataTask()
    {
        // Find all Series that are eligible and limit
        var ids = await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesThatNeedExternalMetadata(25);
        if (ids.Count == 0) return;

        _logger.LogInformation("[Kavita+ Data Refresh] Started Refreshing {Count} series data from Kavita+", ids.Count);
        var count = 0;
        var libTypes = await _unitOfWork.LibraryRepository.GetLibraryTypesBySeriesIdsAsync(ids);
        foreach (var seriesId in ids)
        {
            var libraryType = libTypes[seriesId];
            var success = await FetchSeriesMetadata(seriesId, libraryType);
            if (success) count++;
            await Task.Delay(1500);
        }
        _logger.LogInformation("[Kavita+ Data Refresh] Finished Refreshing {Count} series data from Kavita+", count);
    }


    /// <summary>
    /// Fetches data from Kavita+
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    public async Task<bool> FetchSeriesMetadata(int seriesId, LibraryType libraryType)
    {
        if (!IsPlusEligible(libraryType)) return false;
        if (!await _licenseService.HasActiveLicense()) return false;

        // Generate key based on seriesId and libraryType or any unique identifier for the request
        // Check if the request is allowed based on the rate limit
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            // Request not allowed due to rate limit
            _logger.LogDebug("Rate Limit hit for Kavita+ prefetch");
            return false;
        }

        _logger.LogDebug("Prefetching Kavita+ data for Series {SeriesId}", seriesId);

        // Prefetch SeriesDetail data
        await GetSeriesDetailPlus(seriesId, libraryType);
        return true;
    }

    public async Task<IList<MalStackDto>> GetStacksForUser(int userId)
    {
        if (!await _licenseService.HasActiveLicense()) return ArraySegment<MalStackDto>.Empty;

        // See if this user has Mal account on record
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.MalUserName) || string.IsNullOrEmpty(user.MalAccessToken))
        {
            _logger.LogInformation("User is attempting to fetch MAL Stacks, but missing information on their account");
            return ArraySegment<MalStackDto>.Empty;
        }
        try
        {
            _logger.LogDebug("Fetching Kavita+ for MAL Stacks for user {UserName}", user.MalUserName);

            var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
            var result = await ($"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/stacks?username={user.MalUserName}")
                .WithKavitaPlusHeaders(license)
                .GetJsonAsync<IList<MalStackDto>>();

            if (result == null)
            {
                return ArraySegment<MalStackDto>.Empty;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fetching Kavita+ for MAL Stacks for user {UserName} failed", user.MalUserName);
            return ArraySegment<MalStackDto>.Empty;
        }
    }

    /// <summary>
    /// Returns the match results for a Series from UI Flow
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    public async Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto)
    {
        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId,
            SeriesIncludes.Metadata | SeriesIncludes.ExternalMetadata);
        if (series == null) return [];

        var potentialAnilistId = ScrobblingService.ExtractId<int?>(dto.Query, ScrobblingService.AniListWeblinkWebsite);
        var potentialMalId = ScrobblingService.ExtractId<long?>(dto.Query, ScrobblingService.MalWeblinkWebsite);

        List<string> altNames = [series.LocalizedName, series.OriginalName];
        if (potentialAnilistId == null && potentialMalId == null && !string.IsNullOrEmpty(dto.Query))
        {
            altNames.Add(dto.Query);
        }

        var matchRequest = new MatchSeriesRequestDto()
        {
            Format = series.Format == MangaFormat.Epub ? PlusMediaFormat.LightNovel : PlusMediaFormat.Manga,
            Query = dto.Query,
            SeriesName = series.Name,
            AlternativeNames = altNames.Where(s => !string.IsNullOrEmpty(s)).ToList(),
            Year = series.Metadata.ReleaseYear,
            AniListId = potentialAnilistId ?? ScrobblingService.GetAniListId(series),
            MalId = potentialMalId ?? ScrobblingService.GetMalId(series),
        };

        try
        {
            var results = await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/match-series")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(matchRequest)
                .ReceiveJson<IList<ExternalSeriesMatchDto>>();

            // Some summaries can contain multiple <br/>s, we need to ensure it's only 1
            foreach (var result in results)
            {
                result.Series.Summary = StringHelper.SquashBreaklines(result.Series.Summary);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error happened during the request to Kavita+ API");
        }

        return ArraySegment<ExternalSeriesMatchDto>.Empty;
    }


    /// <summary>
    /// Retrieves Metadata about a Recommended External Series
    /// </summary>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId)
    {
        if (!aniListId.HasValue && !malId.HasValue)
        {
            throw new KavitaException("Unable to find valid information from url for External Load");
        }

        // This is for the Series drawer. We can get this extra information during the initial SeriesDetail call so it's all coming from the DB

        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
        var details = await GetSeriesDetail(license, aniListId, malId, seriesId);

        return details;

    }

    /// <summary>
    /// Returns Series Detail data from Kavita+ - Review, Recs, Ratings
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <returns></returns>
    public async Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType)
    {
        if (!IsPlusEligible(libraryType) || !await _licenseService.HasActiveLicense()) return _defaultReturn;

        // Check blacklist (bad matches) or if there is a don't match
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null || !series.WillScrobble()) return _defaultReturn;

        var needsRefresh =
            await _unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(seriesId);

        if (!needsRefresh)
        {
            // Convert into DTOs and return
            return await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(seriesId);
        }

        var data = await _unitOfWork.SeriesRepository.GetPlusSeriesDto(seriesId);
        if (data == null) return _defaultReturn;

        // Get from Kavita+ API the Full Series metadata with rec/rev and cache to ExternalMetadata tables
        return await FetchExternalMetadataForSeries(seriesId, libraryType, data);
    }

    /// <summary>
    /// This will override any sort of matching that was done prior and force it to be what the user Selected
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="anilistId"></param>
    public async Task FixSeriesMatch(int seriesId, int anilistId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library);
        if (series == null) return;

        // Remove from Blacklist
        series.IsBlacklisted = false;
        series.DontMatch = false;
        _unitOfWork.SeriesRepository.Update(series);

        // Refetch metadata with a Direct lookup
        var metadata = await FetchExternalMetadataForSeries(seriesId, series.Library.Type, new PlusSeriesRequestDto()
        {
            AniListId = anilistId,
            SeriesName = string.Empty // Required field
        });

        if (metadata.Series == null)
        {
            _logger.LogError("Unable to Match {SeriesName} with Kavita+ Series AniList Id: {AniListId}", series.Name, anilistId);
            return;
        }

        // Find all scrobble events and rewrite them to be the correct
        var events = await _unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);
        _unitOfWork.ScrobbleRepository.Remove(events);
        await _unitOfWork.CommitAsync();

        // Regenerate all events for the series for all users
        BackgroundJob.Enqueue(() => _scrobblingService.CreateEventsFromExistingHistoryForSeries(seriesId));

        // Name can be null on Series even with a direct match
        _logger.LogInformation("Matched {SeriesName} with Kavita+ Series {MatchSeriesName}", series.Name, metadata.Series.Name);
    }

    /// <summary>
    /// Sets a series to Dont Match and removes all previously cached
    /// </summary>
    /// <param name="seriesId"></param>
    public async Task UpdateSeriesDontMatch(int seriesId, bool dontMatch)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.ExternalMetadata);
        if (series == null) return;

        _logger.LogInformation("User has asked Kavita to stop matching/scrobbling on {SeriesName}", series.Name);

        series.DontMatch = dontMatch;

        if (dontMatch)
        {
            // When we set as DontMatch, we will clear existing External Metadata
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(series.ExternalSeriesMetadata);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalReviews);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRatings);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRecommendations);
        }

        _unitOfWork.SeriesRepository.Update(series);

        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Requests the full SeriesDetail (rec, review, metadata) data for a Series. Will save to ExternalMetadata tables.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    private async Task<SeriesDetailPlusDto> FetchExternalMetadataForSeries(int seriesId, LibraryType libraryType, PlusSeriesRequestDto data)
    {

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library);
        if (series == null) return _defaultReturn;

        try
        {
            _logger.LogDebug("Fetching Kavita+ Series Detail data for {SeriesName}", string.IsNullOrEmpty(data.SeriesName) ? data.AniListId : data.SeriesName);
            var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
            var result = await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-detail")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(data)
                .ReceiveJson<SeriesDetailPlusApiDto>(); // This returns an AniListSeries and Match returns ExternalSeriesDto


            // Clear out existing results
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalReviews);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRatings);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRecommendations);

            externalSeriesMetadata.ExternalReviews = result.Reviews.Select(r =>
            {
                var review = _mapper.Map<ExternalReview>(r);
                review.SeriesId = externalSeriesMetadata.SeriesId;
                return review;
            }).ToList();

            externalSeriesMetadata.ExternalRatings = result.Ratings.Select(r =>
            {
                var rating = _mapper.Map<ExternalRating>(r);
                rating.SeriesId = externalSeriesMetadata.SeriesId;
                return rating;
            }).ToList();


            // Recommendations
            externalSeriesMetadata.ExternalRecommendations ??= new List<ExternalRecommendation>();
            var recs = await ProcessRecommendations(libraryType, result.Recommendations, externalSeriesMetadata);

            var extRatings = externalSeriesMetadata.ExternalRatings
                .Where(r => r.AverageScore > 0)
                .ToList();

            externalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.Add(_externalSeriesMetadataCache);
            externalSeriesMetadata.AverageExternalRating = extRatings.Count != 0 ? (int) extRatings
                .Average(r => r.AverageScore) : 0;

            if (result.MalId.HasValue) externalSeriesMetadata.MalId = result.MalId.Value;
            if (result.AniListId.HasValue) externalSeriesMetadata.AniListId = result.AniListId.Value;

            // If there is metadata and the user has metadata download turned on
            var madeMetadataModification = false;
            if (result.Series != null && series.Library.AllowMetadataMatching)
            {
                externalSeriesMetadata.Series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);

                madeMetadataModification = await WriteExternalMetadataToSeries(result.Series, seriesId);
                if (madeMetadataModification)
                {
                    _unitOfWork.SeriesRepository.Update(series);
                }
            }

            // WriteExternalMetadataToSeries will commit but not always
            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
            }

            if (madeMetadataModification)
            {
                // Inform the UI of the update
                await _eventHub.SendMessageAsync(MessageFactory.ScanSeries, MessageFactory.ScanSeriesEvent(series.LibraryId, series.Id, series.Name), false);
            }

            return new SeriesDetailPlusDto()
            {
                Recommendations = recs,
                Ratings = result.Ratings,
                Reviews = externalSeriesMetadata.ExternalReviews.Select(r => _mapper.Map<UserReviewDto>(r)),
                Series = result.Series
            };
        }
        catch (FlurlHttpException ex)
        {
            if (ex.StatusCode == 500)
            {
                return _defaultReturn;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to fetch external series metadata from Kavita+");
        }

        // Blacklist the series as it wasn't found in Kavita+
        series.IsBlacklisted = true;
        await _unitOfWork.CommitAsync();

        return _defaultReturn;
    }

    /// <summary>
    /// Given external metadata from Kavita+, write as much as possible to the Kavita series as possible
    /// </summary>
    /// <param name="externalMetadata"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId)
    {
        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
        if (!settings.Enabled) return false;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Related);
        if (series == null) return false;

        var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser();

        _logger.LogInformation("Writing External metadata to Series {SeriesName}", series.Name);

        var madeModification = false;
        var processedGenres = new List<string>();
        var processedTags = new List<string>();

        madeModification = UpdateSummary(series, settings, externalMetadata) || madeModification;
        madeModification = UpdateReleaseYear(series, settings, externalMetadata) || madeModification;
        madeModification = UpdateLocalizedName(series, settings, externalMetadata) || madeModification;
        madeModification = await UpdatePublicationStatus(series, settings, externalMetadata) || madeModification;

        // Apply field mappings
        GenerateGenreAndTagLists(externalMetadata, settings, ref processedTags, ref processedGenres);

        madeModification = await UpdateGenres(series, settings, externalMetadata, processedGenres) || madeModification;
        madeModification = await UpdateTags(series, settings, externalMetadata, processedTags) || madeModification;
        madeModification = UpdateAgeRating(series, settings, processedGenres.Concat(processedTags)) || madeModification;

        var staff = (externalMetadata.Staff ?? []).Select(s =>
        {
            s.Name = settings.FirstLastPeopleNaming ? $"{s.FirstName} {s.LastName}" : $"{s.LastName} {s.FirstName}";

            return s;
        }).ToList();
        madeModification = await UpdateWriters(series, settings, staff) || madeModification;
        madeModification = await UpdateArtists(series, settings, staff) || madeModification;
        madeModification = await UpdateCharacters(series, settings, externalMetadata.Characters) || madeModification;

        madeModification = await UpdateRelationships(series, settings, externalMetadata.Relations, defaultAdmin) || madeModification;
        madeModification = await UpdateCoverImage(series, settings, externalMetadata) || madeModification;

        return madeModification;
    }

    private static void GenerateGenreAndTagLists(ExternalSeriesDetailDto externalMetadata, MetadataSettingsDto settings,
        ref List<string> processedTags, ref List<string> processedGenres)
    {
        externalMetadata.Tags ??= [];
        externalMetadata.Genres ??= [];

        var mappings = ApplyFieldMappings(externalMetadata.Tags.Select(t => t.Name), MetadataFieldType.Tag, settings.FieldMappings);
        if (mappings.TryGetValue(MetadataFieldType.Tag, out var tagsToTags))
        {
            processedTags.AddRange(tagsToTags);
        }
        if (mappings.TryGetValue(MetadataFieldType.Genre, out var tagsToGenres))
        {
            processedGenres.AddRange(tagsToGenres);
        }

        mappings = ApplyFieldMappings(externalMetadata.Genres, MetadataFieldType.Genre, settings.FieldMappings);
        if (mappings.TryGetValue(MetadataFieldType.Tag, out var genresToTags))
        {
            processedTags.AddRange(genresToTags);
        }
        if (mappings.TryGetValue(MetadataFieldType.Genre, out var genresToGenres))
        {
            processedGenres.AddRange(genresToGenres);
        }

        processedTags = ApplyBlackWhiteList(settings, MetadataFieldType.Tag, processedTags);
        processedGenres = ApplyBlackWhiteList(settings, MetadataFieldType.Genre, processedGenres);
    }

    private async Task<bool> UpdateRelationships(Series series, MetadataSettingsDto settings, IList<SeriesRelationship>? externalMetadataRelations, AppUser defaultAdmin)
    {
        if (!settings.EnableRelationships) return false;

        if (externalMetadataRelations == null || externalMetadataRelations.Count == 0 || defaultAdmin == null)
        {
            return false;
        }

        foreach (var relation in externalMetadataRelations)
        {
            var names = new [] {relation.SeriesName.PreferredTitle, relation.SeriesName.RomajiTitle, relation.SeriesName.EnglishTitle, relation.SeriesName.NativeTitle};
            var relatedSeries = await _unitOfWork.SeriesRepository.GetSeriesByAnyName(
                names,
                relation.PlusMediaFormat.GetMangaFormats(),
                defaultAdmin.Id,
                relation.AniListId,
                SeriesIncludes.Related);

            // Skip if no related series found or series is the parent
            if (relatedSeries == null || relatedSeries.Id == series.Id || relation.Relation == RelationKind.Parent) continue;

            // Check if the relationship already exists
            var relationshipExists = series.Relations.Any(r =>
                r.TargetSeriesId == relatedSeries.Id && r.RelationKind == relation.Relation);

            if (relationshipExists) continue;

            // Add new relationship
            var newRelation = new SeriesRelation
            {
                RelationKind = relation.Relation,
                TargetSeriesId = relatedSeries.Id,
                SeriesId = series.Id,
            };
            series.Relations.Add(newRelation);

            // Handle sequel/prequel: add reverse relationship
            if (relation.Relation is RelationKind.Prequel or RelationKind.Sequel)
            {
                var reverseExists = relatedSeries.Relations.Any(r =>
                    r.TargetSeriesId == series.Id && r.RelationKind == GetReverseRelation(relation.Relation));

                if (!reverseExists)
                {
                    var reverseRelation = new SeriesRelation
                    {
                        RelationKind = GetReverseRelation(relation.Relation),
                        TargetSeriesId = series.Id,
                        SeriesId = relatedSeries.Id,
                    };
                    relatedSeries.Relations.Add(reverseRelation);
                    _unitOfWork.SeriesRepository.Attach(reverseRelation);
                }
            }

            _unitOfWork.SeriesRepository.Update(series);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        return true;
    }

    private async Task<bool> UpdateCharacters(Series series, MetadataSettingsDto settings, IList<SeriesCharacter>? externalCharacters)
    {
        if (!settings.EnablePeople) return false;

        if (externalCharacters == null || externalCharacters.Count == 0) return false;

        if (series.Metadata.CharacterLocked && !settings.HasOverride(MetadataSettingField.People))
        {
            return false;
        }

        if (!settings.IsPersonAllowed(PersonRole.Character))
        {
            return false;
        }

        series.Metadata.People ??= [];

        var characters = externalCharacters
            .Select(w => new PersonDto()
            {
                Name = w.Name,
                AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListCharacterWebsite),
                Description = StringHelper.SquashBreaklines(w.Description),
            })
            .Concat(series.Metadata.People
                .Where(p => p.Role == PersonRole.Character)
                // Need to ensure existing people are retained, but we overwrite anything from a bad match
                .Where(p => !p.KavitaPlusConnection)
                .Select(p => _mapper.Map<PersonDto>(p.Person))
            )
            .DistinctBy(p => Parser.Normalize(p.Name))
            .ToList();

        if (characters.Count == 0) return false;
        await SeriesService.HandlePeopleUpdateAsync(series.Metadata, characters, PersonRole.Character, _unitOfWork);
        foreach (var spPerson in series.Metadata.People.Where(p => p.Role == PersonRole.Character))
        {
            // Set a sort order based on their role
            var characterMeta = externalCharacters.FirstOrDefault(c => c.Name == spPerson.Person.Name);
            spPerson.OrderWeight = 0;

            if (characterMeta != null)
            {
                spPerson.KavitaPlusConnection = true;

                spPerson.OrderWeight = characterMeta.Role switch
                {
                    CharacterRole.Main => 0,
                    CharacterRole.Supporting => 1,
                    CharacterRole.Background => 2,
                    _ => 99 // Default for unknown roles
                };
            }
        }

        // Download the image and save it
        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync();

        foreach (var character in externalCharacters)
        {
            var aniListId = ScrobblingService.ExtractId<int>(character.Url, ScrobblingService.AniListCharacterWebsite);
            if (aniListId <= 0) continue;
            var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId);
            if (person != null && !string.IsNullOrEmpty(character.ImageUrl) && string.IsNullOrEmpty(person.CoverImage))
            {
                await _coverDbService.SetPersonCoverByUrl(person, character.ImageUrl, false);
            }
        }


        return true;
    }

    private async Task<bool> UpdateArtists(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return false;


        var upstreamArtists = staff
            .Where(s => s.Role is "Art" or "Story & Art")
            .ToList();

        if (upstreamArtists.Count == 0) return false;

        if (series.Metadata.CoverArtistLocked && !settings.HasOverride(MetadataSettingField.People))
        {
            return false;
        }

        if (!settings.IsPersonAllowed(PersonRole.CoverArtist))
        {
            return false;
        }

        series.Metadata.People ??= [];
        var artists = upstreamArtists
            .Select(w => new PersonDto()
            {
                Name = w.Name,
                AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListStaffWebsite),
                Description = StringHelper.SquashBreaklines(w.Description),
            })
            .Concat(series.Metadata.People
                .Where(p => p.Role == PersonRole.CoverArtist)
                .Where(p => !p.KavitaPlusConnection)
                .Select(p => _mapper.Map<PersonDto>(p.Person))
            )
            .DistinctBy(p => Parser.Normalize(p.Name))
            .ToList();

        await SeriesService.HandlePeopleUpdateAsync(series.Metadata, artists, PersonRole.CoverArtist, _unitOfWork);

        foreach (var person in series.Metadata.People.Where(p => p.Role == PersonRole.CoverArtist))
        {
            var meta = upstreamArtists.FirstOrDefault(c => c.Name == person.Person.Name);
            person.OrderWeight = 0;
            if (meta != null)
            {
                person.KavitaPlusConnection = true;
            }
        }

        // Download the image and save it
        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync();

        await DownloadAndSetPersonCovers(upstreamArtists);

        return true;
    }

    private async Task<bool> UpdateWriters(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return false;

        var upstreamWriters = staff
            .Where(s => s.Role is "Story" or "Story & Art")
            .ToList();

        if (upstreamWriters.Count == 0) return false;

        if (series.Metadata.WriterLocked && !settings.HasOverride(MetadataSettingField.People))
        {
            return false;
        }

        if (!settings.IsPersonAllowed(PersonRole.Writer))
        {
            return false;
        }

        series.Metadata.People ??= [];
        var writers = upstreamWriters
            .Select(w => new PersonDto()
            {
                Name = w.Name,
                AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListStaffWebsite),
                Description = StringHelper.SquashBreaklines(w.Description),
            })
            .Concat(series.Metadata.People
                .Where(p => p.Role == PersonRole.Writer)
                .Where(p => !p.KavitaPlusConnection)
                .Select(p => _mapper.Map<PersonDto>(p.Person))
            )
            .DistinctBy(p => Parser.Normalize(p.Name))
            .ToList();


        await SeriesService.HandlePeopleUpdateAsync(series.Metadata, writers, PersonRole.Writer, _unitOfWork);

        foreach (var person in series.Metadata.People.Where(p => p.Role == PersonRole.Writer))
        {
            var meta = upstreamWriters.FirstOrDefault(c => c.Name == person.Person.Name);
            person.OrderWeight = 0;
            if (meta != null)
            {
                person.KavitaPlusConnection = true;
            }
        }

        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync();

        await DownloadAndSetPersonCovers(upstreamWriters);

        return true;
    }

    private async Task<bool> UpdateTags(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata, List<string> processedTags)
    {
        externalMetadata.Tags ??= [];

        if (!settings.EnableTags || processedTags.Count == 0) return false;

        if (series.Metadata.TagsLocked && !settings.HasOverride(MetadataSettingField.Tags))
        {
            return false;
        }

        _logger.LogDebug("Found {TagCount} tags for {SeriesName}", processedTags.Count, series.Name);
        var madeModification = false;
        var allTags = (await _unitOfWork.TagRepository.GetAllTagsByNameAsync(processedTags.Select(Parser.Normalize)))
            .ToList();
        series.Metadata.Tags ??= [];

        TagHelper.UpdateTagList(processedTags, series, allTags, tag =>
        {
            series.Metadata.Tags.Add(tag);
            madeModification = true;
        }, () => series.Metadata.TagsLocked = true);

        return madeModification;
    }

    private static List<string> ApplyBlackWhiteList(MetadataSettingsDto settings, MetadataFieldType fieldType, List<string> processedStrings)
    {
        return fieldType switch
        {
            MetadataFieldType.Genre => processedStrings.Distinct()
                .Where(g => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(g))
                .ToList(),
            MetadataFieldType.Tag => processedStrings.Distinct()
                .Where(g => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(g))
                .Where(g => settings.Whitelist.Count == 0 || settings.Whitelist.Contains(g))
                .ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(fieldType), fieldType, null)
        };
    }

    private async Task<bool> UpdateGenres(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata, List<string> processedGenres)
    {
        externalMetadata.Genres ??= [];

        if (!settings.EnableGenres || processedGenres.Count == 0) return false;

        if (series.Metadata.GenresLocked && !settings.HasOverride(MetadataSettingField.Genres))
        {
            return false;
        }

        _logger.LogDebug("Found {GenreCount} genres for {SeriesName}", processedGenres.Count, series.Name);
        var madeModification = false;
        var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(processedGenres.Select(Parser.Normalize))).ToList();
        series.Metadata.Genres ??= [];
        var exisitingGenres = series.Metadata.Genres;

        GenreHelper.UpdateGenreList(processedGenres, series, allGenres, genre =>
        {
            series.Metadata.Genres.Add(genre);
            madeModification = true;
        }, () => series.Metadata.GenresLocked = true);

        foreach (var genre in exisitingGenres)
        {
            if (series.Metadata.Genres.FirstOrDefault(g => g.NormalizedTitle == genre.NormalizedTitle) != null) continue;
            series.Metadata.Genres.Add(genre);
        }

        return madeModification;
    }

    private async Task<bool> UpdatePublicationStatus(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnablePublicationStatus) return false;

        if (series.Metadata.PublicationStatusLocked && !settings.HasOverride(MetadataSettingField.PublicationStatus))
        {
            return false;
        }

        try
        {
            var chapters =
                (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(series.Id, SeriesIncludes.Chapters))!.Volumes
                .SelectMany(v => v.Chapters).ToList();
            var status = DeterminePublicationStatus(series, chapters, externalMetadata);

            series.Metadata.PublicationStatus = status;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue determining Publication Status for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }

        return false;
    }

    private bool UpdateAgeRating(Series series, MetadataSettingsDto settings, IEnumerable<string> allExternalTags)
    {

        if (series.Metadata.AgeRatingLocked && !settings.HasOverride(MetadataSettingField.AgeRating))
        {
            return false;
        }

        try
        {
            // Determine Age Rating
            var totalTags = allExternalTags
                .Concat(series.Metadata.Genres.Select(g => g.Title))
                .Concat(series.Metadata.Tags.Select(g => g.Title));

            var ageRating = DetermineAgeRating(totalTags, settings.AgeRatingMappings);
            if (series.Metadata.AgeRating <= ageRating)
            {
                series.Metadata.AgeRating = ageRating;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue determining Age Rating for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }

        return false;
    }

    private async Task<bool> UpdateCoverImage(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableCoverImage) return false;

        if (string.IsNullOrEmpty(externalMetadata.CoverUrl)) return false;

        if (series.CoverImageLocked && !settings.HasOverride(MetadataSettingField.Covers))
        {
            return false;
        }

        if (string.IsNullOrEmpty(externalMetadata.CoverUrl))
        {
            return false;
        }

        await DownloadSeriesCovers(series, externalMetadata.CoverUrl);
        return true;
    }


    private static bool UpdateReleaseYear(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableStartDate) return false;

        if (!externalMetadata.StartDate.HasValue) return false;

        if (series.Metadata.ReleaseYearLocked && !settings.HasOverride(MetadataSettingField.StartDate))
        {
            return false;
        }

        if (series.Metadata.ReleaseYear != 0 && !settings.HasOverride(MetadataSettingField.StartDate))
        {
            return false;
        }

        series.Metadata.ReleaseYear = externalMetadata.StartDate.Value.Year;
        return true;
    }

    private static bool UpdateLocalizedName(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableLocalizedName) return false;

        if (series.LocalizedNameLocked && !settings.HasOverride(MetadataSettingField.LocalizedName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(series.LocalizedName) && !settings.HasOverride(MetadataSettingField.LocalizedName))
        {
            return false;
        }

        // We need to make the best appropriate guess
        if (externalMetadata.Name == series.Name)
        {
            // Choose closest (usually last) synonym
            var validSynonyms = externalMetadata.Synonyms
                .Where(IsRomanCharacters)
                .Where(s => s.ToNormalized() != series.Name.ToNormalized())
                .ToList();

            if (validSynonyms.Count == 0) return false;

            series.LocalizedName = validSynonyms[^1];
            series.LocalizedNameLocked = true;
        }
        else if (IsRomanCharacters(externalMetadata.Name))
        {
            series.LocalizedName = externalMetadata.Name;
            series.LocalizedNameLocked = true;
        }


        return true;
    }

    private static bool UpdateSummary(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableSummary) return false;

        if (string.IsNullOrEmpty(externalMetadata.Summary)) return false;

        if (series.Metadata.SummaryLocked && !settings.HasOverride(MetadataSettingField.Summary))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(series.Metadata.Summary) && !settings.HasOverride(MetadataSettingField.Summary))
        {
            return false;
        }

        series.Metadata.Summary = StringHelper.SquashBreaklines(externalMetadata.Summary);
        return true;
    }


    private static RelationKind GetReverseRelation(RelationKind relation)
    {
        return relation switch
        {
            RelationKind.Prequel => RelationKind.Sequel,
            RelationKind.Sequel => RelationKind.Prequel,
            _ => relation // For other relationships, no reverse needed
        };
    }

    private async Task DownloadSeriesCovers(Series series, string coverUrl)
    {
        try
        {
            await _coverDbService.SetSeriesCoverByUrl(series, coverUrl, false, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception downloading cover image for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }
    }

    private async Task DownloadAndSetPersonCovers(List<SeriesStaffDto> people)
    {
        foreach (var staff in people)
        {
            var aniListId = ScrobblingService.ExtractId<int?>(staff.Url, ScrobblingService.AniListStaffWebsite);
            if (aniListId is null or <= 0) continue;
            var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId.Value);
            if (person != null && !string.IsNullOrEmpty(staff.ImageUrl) && string.IsNullOrEmpty(person.CoverImage))
            {
                await _coverDbService.SetPersonCoverByUrl(person, staff.ImageUrl, false, true);
            }
        }
    }

    private PublicationStatus DeterminePublicationStatus(Series series, List<Chapter> chapters, ExternalSeriesDetailDto externalMetadata)
    {
        try
        {
            // Determine the expected total count based on local metadata
            series.Metadata.TotalCount = Math.Max(
                chapters.Max(chapter => chapter.TotalCount),
                externalMetadata.Volumes > 0 ? externalMetadata.Volumes : externalMetadata.Chapters
            );

            // The actual number of count's defined across all chapter's metadata
            series.Metadata.MaxCount = chapters.Max(chapter => chapter.Count);

            var nonSpecialVolumes = series.Volumes
                .Where(v => v.MaxNumber.IsNot(Parser.SpecialVolumeNumber))
                .ToList();

            var maxVolume = (int)(nonSpecialVolumes.Count != 0 ? nonSpecialVolumes.Max(v => v.MaxNumber) : 0);
            var maxChapter = (int)chapters.Max(c => c.MaxNumber);

            if (series.Format == MangaFormat.Epub || series.Format == MangaFormat.Pdf && chapters.Count == 1)
            {
                series.Metadata.MaxCount = 1;
            }
            else if (series.Metadata.TotalCount <= 1 && chapters.Count == 1 && chapters[0].IsSpecial)
            {
                series.Metadata.MaxCount = series.Metadata.TotalCount;
            }
            else if ((maxChapter == Parser.DefaultChapterNumber || maxChapter > series.Metadata.TotalCount) &&
                     maxVolume <= series.Metadata.TotalCount)
            {
                series.Metadata.MaxCount = maxVolume;
            }
            else if (maxVolume == series.Metadata.TotalCount)
            {
                series.Metadata.MaxCount = maxVolume;
            }
            else
            {
                series.Metadata.MaxCount = maxChapter;
            }

            var status = PublicationStatus.OnGoing;

            var hasExternalCounts = externalMetadata.Volumes > 0 || externalMetadata.Chapters > 0;

            if (hasExternalCounts)
            {
                status = PublicationStatus.Ended;

                // Check if all volumes/chapters match the total count
                if (series.Metadata.MaxCount == series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
                {
                    status = PublicationStatus.Completed;
                }
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an issue determining Publication Status");
        }

        return PublicationStatus.OnGoing;
    }

    private static Dictionary<MetadataFieldType, List<string>> ApplyFieldMappings(IEnumerable<string> values, MetadataFieldType sourceType, List<MetadataFieldMappingDto> mappings)
    {
        var result = new Dictionary<MetadataFieldType, List<string>>();

        foreach (var field in Enum.GetValues<MetadataFieldType>())
        {
            result[field] = [];
        }

        foreach (var value in values)
        {
            var mapping = mappings.FirstOrDefault(m =>
                m.SourceType == sourceType &&
                m.SourceValue.Equals(value, StringComparison.OrdinalIgnoreCase));

            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.DestinationValue))
            {
                var targetType = mapping.DestinationType;

                if (!mapping.ExcludeFromSource)
                {
                    result[sourceType].Add(mapping.SourceValue);
                }

                result[targetType].Add(mapping.DestinationValue);
            }
            else
            {
                // If no mapping, keep the original value
                result[sourceType].Add(value);
            }
        }

        // Ensure distinct
        foreach (var key in result.Keys)
        {
            result[key] = result[key].Distinct().ToList();
        }

        return result;
    }


    /// <summary>
    /// Returns the highest age rating from all tags/genres based on user-supplied mappings
    /// </summary>
    /// <param name="values">A combo of all tags/genres</param>
    /// <param name="mappings"></param>
    /// <returns></returns>
    public static AgeRating DetermineAgeRating(IEnumerable<string> values, Dictionary<string, AgeRating> mappings)
    {
        // Find highest age rating from mappings
        mappings ??= new Dictionary<string, AgeRating>();

        return values
            .Select(v => mappings.TryGetValue(v, out var mapping) ? mapping : AgeRating.Unknown)
            .DefaultIfEmpty(AgeRating.Unknown)
            .Max();
    }


    /// <summary>
    /// Gets from DB or creates a new one with just SeriesId
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="series"></param>
    /// <returns></returns>
    private async Task<ExternalSeriesMetadata> GetOrCreateExternalSeriesMetadataForSeries(int seriesId, Series series)
    {
        var externalSeriesMetadata = await _unitOfWork.ExternalSeriesMetadataRepository.GetExternalSeriesMetadata(seriesId);
        if (externalSeriesMetadata != null) return externalSeriesMetadata;

        externalSeriesMetadata = new ExternalSeriesMetadata()
        {
            SeriesId = seriesId,
        };
        series.ExternalSeriesMetadata = externalSeriesMetadata;
        _unitOfWork.ExternalSeriesMetadataRepository.Attach(externalSeriesMetadata);

        return externalSeriesMetadata;
    }

    private async Task<RecommendationDto> ProcessRecommendations(LibraryType libraryType, IEnumerable<MediaRecommendationDto> recs,
        ExternalSeriesMetadata externalSeriesMetadata)
    {
        var recDto = new RecommendationDto()
        {
            ExternalSeries = new List<ExternalSeriesDto>(),
            OwnedSeries = new List<SeriesDto>()
        };

        // NOTE: This can result in a series being recommended that shares the same name but different format
        foreach (var rec in recs)
        {
            // Find the series based on name and type and that the user has access too
            var seriesForRec = await _unitOfWork.SeriesRepository.GetSeriesDtoByNamesAndMetadataIds(rec.RecommendationNames,
                libraryType, ScrobblingService.CreateUrl(ScrobblingService.AniListWeblinkWebsite, rec.AniListId),
                ScrobblingService.CreateUrl(ScrobblingService.MalWeblinkWebsite, rec.MalId));

            if (seriesForRec != null)
            {
                recDto.OwnedSeries.Add(seriesForRec);
                externalSeriesMetadata.ExternalRecommendations.Add(new ExternalRecommendation()
                {
                    SeriesId = seriesForRec.Id,
                    AniListId = rec.AniListId,
                    MalId = rec.MalId,
                    Name = seriesForRec.Name,
                    Url = rec.SiteUrl,
                    CoverUrl = rec.CoverUrl,
                    Summary = rec.Summary,
                    Provider = rec.Provider
                });
                continue;
            }

            // We can show this based on user permissions
            if (string.IsNullOrEmpty(rec.Name) || string.IsNullOrEmpty(rec.SiteUrl) || string.IsNullOrEmpty(rec.CoverUrl)) continue;
            recDto.ExternalSeries.Add(new ExternalSeriesDto()
            {
                Name = string.IsNullOrEmpty(rec.Name) ? rec.RecommendationNames.First() : rec.Name,
                Url = rec.SiteUrl,
                CoverUrl = rec.CoverUrl,
                Summary = rec.Summary,
                AniListId = rec.AniListId,
                MalId = rec.MalId
            });
            externalSeriesMetadata.ExternalRecommendations.Add(new ExternalRecommendation()
            {
                SeriesId = null,
                AniListId = rec.AniListId,
                MalId = rec.MalId,
                Name = rec.Name,
                Url = rec.SiteUrl,
                CoverUrl = rec.CoverUrl,
                Summary = rec.Summary,
                Provider = rec.Provider
            });
        }

        recDto.OwnedSeries = recDto.OwnedSeries.DistinctBy(s => s.Id).OrderBy(r => r.Name).ToList();
        recDto.ExternalSeries = recDto.ExternalSeries.DistinctBy(s => s.Name.ToNormalized()).OrderBy(r => r.Name).ToList();

        return recDto;
    }


    private async Task<ExternalSeriesDetailDto?> GetSeriesDetail(string license, int? aniListId, long? malId, int? seriesId)
    {
        var payload = new ExternalMetadataIdsDto()
        {
            AniListId = aniListId,
            MalId = malId,
            SeriesName = string.Empty,
            LocalizedSeriesName = string.Empty
        };

        if (seriesId is > 0)
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId.Value,
                SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.ExternalReviews);
            if (series != null)
            {
                if (payload.AniListId <= 0)
                {
                    payload.AniListId = ScrobblingService.ExtractId<int>(series.Metadata.WebLinks, ScrobblingService.AniListWeblinkWebsite);
                }
                if (payload.MalId <= 0)
                {
                    payload.MalId = ScrobblingService.ExtractId<long>(series.Metadata.WebLinks, ScrobblingService.MalWeblinkWebsite);
                }
                payload.SeriesName = series.Name;
                payload.LocalizedSeriesName = series.LocalizedName;
                payload.PlusMediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format);
            }

        }
        try
        {
            var ret =  await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-by-ids")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(payload)
                .ReceiveJson<ExternalSeriesDetailDto>();

            ret.Summary = StringHelper.SquashBreaklines(ret.Summary);

            return ret;

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }
}
