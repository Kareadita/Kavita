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
    /// <returns></returns>
    Task FetchSeriesMetadata(int seriesId, LibraryType libraryType);

    Task<IList<MalStackDto>> GetStacksForUser(int userId);
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto);
    Task FixSeriesMatch(int seriesId, int anilistId);
    Task UpdateSeriesDontMatch(int seriesId, bool dontMatch);
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
        var ids = await _unitOfWork.ExternalSeriesMetadataRepository.GetAllSeriesIdsWithoutMetadata(25);
        if (ids.Count == 0) return;

        _logger.LogInformation("[Kavita+ Data Refresh] Started Refreshing {Count} series data from Kavita+", ids.Count);
        var count = 0;
        var libTypes = await _unitOfWork.LibraryRepository.GetLibraryTypesBySeriesIdsAsync(ids);
        foreach (var seriesId in ids)
        {
            var libraryType = libTypes[seriesId];
            await FetchSeriesMetadata(seriesId, libraryType);
            await Task.Delay(1500);
            count++;
        }
        _logger.LogInformation("[Kavita+ Data Refresh] Finished Refreshing {Count} series data from Kavita+", count);
    }


    /// <summary>
    /// Fetches data from Kavita+
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    public async Task FetchSeriesMetadata(int seriesId, LibraryType libraryType)
    {
        if (!IsPlusEligible(libraryType)) return;
        if (!await _licenseService.HasActiveLicense()) return;

        // Generate key based on seriesId and libraryType or any unique identifier for the request
        // Check if the request is allowed based on the rate limit
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            // Request not allowed due to rate limit
            _logger.LogDebug("Rate Limit hit for Kavita+ prefetch");
            return;
        }

        _logger.LogDebug("Prefetching Kavita+ data for Series {SeriesId}", seriesId);

        // Prefetch SeriesDetail data
        await GetSeriesDetailPlus(seriesId, libraryType);

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
                result.Series.Summary = CleanSummary(result.Series.Summary);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error happened during the request to Kavita+ API");
        }

        return ArraySegment<ExternalSeriesMatchDto>.Empty;
    }

    private static string CleanSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty; // Return as is if null, empty, or whitespace.
        }

        // Remove all variations of <br> tags (case-insensitive)
        summary = Regex.Replace(summary, @"<br\s*/?>", " ", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Normalize whitespace (replace multiple spaces with a single space)
        summary = Regex.Replace(summary, @"\s+", " ").Trim();

        return summary;
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
        // await _eventHub.SendMessageAsync(MessageFactory.Info,
        //     MessageFactory.InfoEvent($"Fix Match: {series.Name}", "Scrobble Events are regenerating with the new match"));


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
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series!);
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
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series!);
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


            await _unitOfWork.CommitAsync();

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
    private async Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId)
    {
        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
        if (!settings.Enabled) return false;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Related);
        if (series == null) return false;

        var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser();

        _logger.LogInformation("Writing External metadata to Series {SeriesName}", series.Name);

        var madeModification = false;

        if (settings.EnableLocalizedName && (!series.LocalizedNameLocked || settings.HasOverride(MetadataSettingField.LocalizedName)))
        {
            // We need to make the best appropriate guess
            if (externalMetadata.Name == series.Name)
            {
                // Choose closest (usually last) synonym
                series.LocalizedName = externalMetadata.Synonyms.Last();
            }
            else
            {
                series.LocalizedName = externalMetadata.Name;
            }

            madeModification = true;
        }

        if (settings.EnableSummary && (!series.Metadata.SummaryLocked ||
                                       settings.HasOverride(MetadataSettingField.Summary)))
        {
            series.Metadata.Summary = CleanSummary(externalMetadata.Summary);
            madeModification = true;
        }

        if (settings.EnableStartDate && externalMetadata.StartDate.HasValue && (!series.Metadata.ReleaseYearLocked ||
                settings.HasOverride(MetadataSettingField.StartDate)))
        {
            series.Metadata.ReleaseYear = externalMetadata.StartDate.Value.Year;
            madeModification = true;
        }

        var processedGenres = new List<string>();
        var processedTags = new List<string>();

        #region Genres and Tags

        // Process Genres
        if (externalMetadata.Genres != null)
        {
            foreach (var genre in externalMetadata.Genres)
            {
                // Apply field mappings
                var mappedGenre = ApplyFieldMapping(genre, MetadataFieldType.Genre, settings.FieldMappings);
                if (mappedGenre != null)
                {
                    processedGenres.Add(mappedGenre);
                }
            }

            // Strip blacklisted items from processedGenres
            processedGenres = processedGenres
                .Distinct()
                .Where(g => !settings.Blacklist.Contains(g))
                .ToList();

            if (settings.EnableGenres && processedGenres.Count > 0 && (!series.Metadata.GenresLocked || settings.HasOverride(MetadataSettingField.Genres)))
            {
                _logger.LogDebug("Found {GenreCount} genres for {SeriesName}", processedGenres.Count, series.Name);
                var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(processedGenres.Select(Parser.Normalize))).ToList();
                series.Metadata.Genres ??= [];
                GenreHelper.UpdateGenreList(processedGenres, series, allGenres, genre =>
                {
                    series.Metadata.Genres.Add(genre);
                    madeModification = true;
                }, () => series.Metadata.GenresLocked = true);
            }

        }

        // Process Tags
        if (externalMetadata.Tags != null)
        {
            foreach (var tag in externalMetadata.Tags.Select(t => t.Name))
            {
                // Apply field mappings
                var mappedTag = ApplyFieldMapping(tag, MetadataFieldType.Tag, settings.FieldMappings);
                if (mappedTag != null)
                {
                    processedTags.Add(mappedTag);
                }
            }

            // Strip blacklisted items from processedTags
            processedTags = processedTags
                .Distinct()
                .Where(g => !settings.Blacklist.Contains(g))
                .Where(g => settings.Whitelist.Count == 0 || settings.Whitelist.Contains(g))
                .ToList();

            // Set the tags for the series and ensure they are in the DB
            if (settings.EnableTags && processedTags.Count > 0 && (!series.Metadata.TagsLocked || settings.HasOverride(MetadataSettingField.Tags)))
            {
                _logger.LogDebug("Found {TagCount} tags for {SeriesName}", processedTags.Count, series.Name);
                var allTags = (await _unitOfWork.TagRepository.GetAllTagsByNameAsync(processedTags.Select(Parser.Normalize)))
                    .ToList();
                series.Metadata.Tags ??= [];
                TagHelper.UpdateTagList(processedTags, series, allTags, tag =>
                {
                    series.Metadata.Tags.Add(tag);
                    madeModification = true;
                }, () => series.Metadata.TagsLocked = true);
            }
        }

        #endregion

        #region Age Rating

        if (!series.Metadata.AgeRatingLocked || settings.HasOverride(MetadataSettingField.AgeRating))
        {
            try
            {
                // Determine Age Rating
                var totalTags = processedGenres
                    .Concat(processedTags)
                    .Concat(series.Metadata.Genres.Select(g => g.Title))
                    .Concat(series.Metadata.Tags.Select(g => g.Title));

                var ageRating = DetermineAgeRating(totalTags, settings.AgeRatingMappings);
                if (!series.Metadata.AgeRatingLocked && series.Metadata.AgeRating <= ageRating)
                {
                    series.Metadata.AgeRating = ageRating;
                    _unitOfWork.SeriesRepository.Update(series);
                    madeModification = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue determining Age Rating for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
            }
        }
        #endregion

        #region People

        if (settings.EnablePeople)
        {
            series.Metadata.People ??= [];

            // Ensure all people are named correctly
            externalMetadata.Staff = externalMetadata.Staff.Select(s =>
            {
                if (settings.FirstLastPeopleNaming)
                {
                    s.Name = s.FirstName + " " + s.LastName;
                }
                else
                {
                    s.Name = s.LastName + " " + s.FirstName;
                }

                return s;
            }).ToList();

            // Roles: Character Design, Story, Art

            var upstreamWriters = externalMetadata.Staff
                .Where(s => s.Role is "Story" or "Story & Art")
                .ToList();

            var writers = upstreamWriters
                .Select(w => new PersonDto()
                {
                    Name = w.Name,
                    AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListStaffWebsite),
                    Description = CleanSummary(w.Description),
                })
                .Concat(series.Metadata.People
                    .Where(p => p.Role == PersonRole.Writer)
                    .Where(p => !p.KavitaPlusConnection)
                    .Select(p => _mapper.Map<PersonDto>(p.Person))
                )
                .DistinctBy(p => Parser.Normalize(p.Name))
                .ToList();


            // NOTE: PersonRoles can be a hashset
            if (writers.Count > 0 && settings.IsPersonAllowed(PersonRole.Writer) && (!series.Metadata.WriterLocked || settings.HasOverride(MetadataSettingField.People)))
            {
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

                await DownloadAndSetCovers(upstreamWriters);

                madeModification = true;
            }

            var upstreamArtists = externalMetadata.Staff
                .Where(s => s.Role is "Art" or "Story & Art")
                .ToList();

            var artists = upstreamArtists
                .Select(w => new PersonDto()
                {
                    Name = w.Name,
                    AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListStaffWebsite),
                    Description = CleanSummary(w.Description),
                })
                .Concat(series.Metadata.People
                    .Where(p => p.Role == PersonRole.CoverArtist)
                    .Where(p => !p.KavitaPlusConnection)
                    .Select(p => _mapper.Map<PersonDto>(p.Person))
                )
                .DistinctBy(p => Parser.Normalize(p.Name))
                .ToList();

            if (artists.Count > 0 &&  settings.IsPersonAllowed(PersonRole.CoverArtist) && (!series.Metadata.CoverArtistLocked || settings.HasOverride(MetadataSettingField.People)))
            {
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

                await DownloadAndSetCovers(upstreamArtists);

                madeModification = true;
            }

            if (externalMetadata.Characters != null && settings.IsPersonAllowed(PersonRole.Character) && (!series.Metadata.CharacterLocked ||
                    settings.HasOverride(MetadataSettingField.People)))
            {
                var characters = externalMetadata.Characters
                    .Select(w => new PersonDto()
                    {
                        Name = w.Name,
                        AniListId = ScrobblingService.ExtractId<int>(w.Url, ScrobblingService.AniListCharacterWebsite),
                        Description = CleanSummary(w.Description),
                    })
                    .Concat(series.Metadata.People
                        .Where(p => p.Role == PersonRole.Character)
                        // Need to ensure existing people are retained, but we overwrite anything from a bad match
                        .Where(p => !p.KavitaPlusConnection)
                        .Select(p => _mapper.Map<PersonDto>(p.Person))
                    )
                    .DistinctBy(p => Parser.Normalize(p.Name))
                    .ToList();


                if (characters.Count > 0)
                {
                    await SeriesService.HandlePeopleUpdateAsync(series.Metadata, characters, PersonRole.Character, _unitOfWork);
                    foreach (var spPerson in series.Metadata.People.Where(p => p.Role == PersonRole.Character))
                    {
                        // Set a sort order based on their role
                        var characterMeta = externalMetadata.Characters?.FirstOrDefault(c => c.Name == spPerson.Person.Name);
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

                    foreach (var character in externalMetadata.Characters ?? [])
                    {
                        var aniListId = ScrobblingService.ExtractId<int>(character.Url, ScrobblingService.AniListCharacterWebsite);
                        if (aniListId <= 0) continue;
                        var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId);
                        if (person != null && !string.IsNullOrEmpty(character.ImageUrl) && string.IsNullOrEmpty(person.CoverImage))
                        {
                            await _coverDbService.SetPersonCoverByUrl(person, character.ImageUrl, false);
                        }
                    }

                    madeModification = true;
                }
            }
        }

        #endregion

        #region Publication Status

        if (settings.EnablePublicationStatus && (!series.Metadata.PublicationStatusLocked ||
                settings.HasOverride(MetadataSettingField.PublicationStatus)))
        {
            try
            {
                var chapters =
                    (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(series.Id, SeriesIncludes.Chapters))!.Volumes
                    .SelectMany(v => v.Chapters).ToList();
                var wasChanged = DeterminePublicationStatus(series, chapters, externalMetadata);
                _unitOfWork.SeriesRepository.Update(series);
                madeModification = madeModification || wasChanged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue determining Publication Status for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
            }
        }
        #endregion

        #region Relationships

        if (settings.EnableRelationships && externalMetadata.Relations != null && defaultAdmin != null)
        {
            foreach (var relation in externalMetadata.Relations)
            {
                var relatedSeries = await _unitOfWork.SeriesRepository.GetSeriesByAnyName(
                    relation.SeriesName.NativeTitle,
                    relation.SeriesName.PreferredTitle,
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

                series.Relations.Add(new SeriesRelation
                {
                    RelationKind = relation.Relation,
                    TargetSeries = relatedSeries,
                    TargetSeriesId = relatedSeries.Id,
                    Series = series,
                    SeriesId = series.Id
                });

                // Handle sequel/prequel: add reverse relationship
                if (relation.Relation is RelationKind.Prequel or RelationKind.Sequel)
                {
                    var reverseExists = relatedSeries.Relations.Any(r =>
                        r.TargetSeriesId == series.Id && r.RelationKind == GetReverseRelation(relation.Relation));

                    if (reverseExists) continue;

                    relatedSeries.Relations.Add(new SeriesRelation
                    {
                        RelationKind = GetReverseRelation(relation.Relation),
                        TargetSeries = series,
                        TargetSeriesId = series.Id,
                        Series = relatedSeries,
                        SeriesId = relatedSeries.Id
                    });
                }

                madeModification = true;
            }
        }
        #endregion

        #region Series Cover

        // This must not allow cover image locked to be off after downloading, else it will call every time a match is hit
        if (!string.IsNullOrEmpty(externalMetadata.CoverUrl) && (!series.CoverImageLocked || settings.HasOverride(MetadataSettingField.Covers)))
        {
            await DownloadSeriesCovers(series, externalMetadata.CoverUrl);
        }

        #endregion

        return madeModification;
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
            await _coverDbService.SetSeriesCoverByUrl(series, coverUrl, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception downloading cover image for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }
    }

    private async Task DownloadAndSetCovers(List<SeriesStaffDto> people)
    {
        foreach (var staff in people)
        {
            var aniListId = ScrobblingService.ExtractId<int?>(staff.Url, ScrobblingService.AniListStaffWebsite);
            if (aniListId is null or <= 0) continue;
            var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId.Value);
            if (person != null && !string.IsNullOrEmpty(staff.ImageUrl) && string.IsNullOrEmpty(person.CoverImage))
            {
                await _coverDbService.SetPersonCoverByUrl(person, staff.ImageUrl, false);
            }
        }
    }

    private bool DeterminePublicationStatus(Series series, List<Chapter> chapters, ExternalSeriesDetailDto externalMetadata)
    {
        var madeModification = false;
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

                madeModification = true;
            }

            series.Metadata.PublicationStatus = status;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an issue determining Publication Status");
            series.Metadata.PublicationStatus = PublicationStatus.OnGoing;
        }

        return madeModification;
    }

    private static string? ApplyFieldMapping(string value, MetadataFieldType sourceType, List<MetadataFieldMappingDto> mappings)
    {
        // Find matching mapping
        var mapping = mappings
            .FirstOrDefault(m =>
                m.SourceType == sourceType &&
                m.SourceValue.Equals(value, StringComparison.OrdinalIgnoreCase));

        if (mapping == null) return value;

        // If mapping exists, return destination or source value
        return mapping.DestinationValue ?? (mapping.ExcludeFromSource ? null : value);
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
            return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-by-ids")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(payload)
                .ReceiveJson<ExternalSeriesDetailDto>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }
}
