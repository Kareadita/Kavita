using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Metadata;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Builders;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class ExternalMetadataService : IExternalMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExternalMetadataService> _logger;
    private readonly IMapper _mapper;
    private readonly ILicenseService _licenseService;
    private readonly IScrobblingService _scrobblingService;
    private readonly IEventHub _eventHub;
    private readonly ICoverDbService _coverDbService;
    private readonly IKavitaPlusApiService _kavitaPlusApiService;
    private readonly IFileCacheService _fileCacheService;
    private readonly IKavitaPlusAuditService _auditService;

    private const int SeriesPerRefresh = 25;
    private readonly TimeSpan _externalSeriesMetadataCache = TimeSpan.FromDays(30);
    private static readonly HashSet<LibraryType> NonEligibleLibraryTypes = [LibraryType.Comic, LibraryType.Image];
    private readonly SeriesDetailPlusDto _defaultReturn = new()
    {
        Series =  null,
        Recommendations = null,
        Ratings = [],
        Reviews = []
    };
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);
    private static bool IsRomanCharacters(string input) => Regex.IsMatch(input, @"^[\p{IsBasicLatin}\p{IsLatin-1Supplement}]+$");

    public ExternalMetadataService(IUnitOfWork unitOfWork, ILogger<ExternalMetadataService> logger, IMapper mapper,
        ILicenseService licenseService, IScrobblingService scrobblingService, IEventHub eventHub, ICoverDbService coverDbService,
        IKavitaPlusApiService kavitaPlusApiService, IFileCacheService fileCacheService, IKavitaPlusAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _licenseService = licenseService;
        _scrobblingService = scrobblingService;
        _eventHub = eventHub;
        _coverDbService = coverDbService;
        _kavitaPlusApiService = kavitaPlusApiService;
        _fileCacheService = fileCacheService;
        _auditService = auditService;

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

    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task FetchExternalDataTask(CancellationToken ct = default)
    {
        // Find all Series that are eligible and limit
        var ids = await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesThatNeedExternalMetadata(SeriesPerRefresh, ct: ct);
        if (ids.Count != SeriesPerRefresh)
        {
            var wanted = SeriesPerRefresh - ids.Count;
            ids.AddRange(await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesThatNeedExternalMetadata(wanted, true, ct));
        }

        if (ids.Count == 0)
        {
            _logger.LogInformation("[Kavita+ Data Refresh] No series need matching or refreshing (stale data)");
            return;
        }


        _logger.LogInformation("[Kavita+ Data Refresh] Started Refreshing {Count} series data from Kavita+: {Ids}", ids.Count, string.Join(',', ids));
        var count = 0;
        var successfulMatches = new List<int>();
        var libTypes = await _unitOfWork.LibraryRepository.GetLibraryTypesBySeriesIdsAsync(ids, ct);
        foreach (var seriesId in ids)
        {
            var libraryType = libTypes[seriesId];
            var success = await FetchSeriesMetadata(seriesId, libraryType, MetadataFetchTrigger.ScheduledRefresh, ct);
            if (success)
            {
                count++;
                successfulMatches.Add(seriesId);
            }
            await Task.Delay(10000, ct); // Currently AL is degraded and has 30 requests/min, give a little padding since this is a background request
        }
        _logger.LogInformation("[Kavita+ Data Refresh] Finished Refreshing {Count} / {Total} series data from Kavita+: {Ids}", count, ids.Count, string.Join(',', successfulMatches));
    }


    public async Task<bool> FetchSeriesMetadata(int seriesId, LibraryType libraryType,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.SeriesAdded, CancellationToken ct = default)
    {
        if (!IsPlusEligible(libraryType)) return false;
        if (!await _licenseService.HasActiveLicense(ct: ct)) return false;

        // Generate key based on seriesId and libraryType or any unique identifier for the request
        // Check if the request is allowed based on the rate limit
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            // Request not allowed due to rate limit
            _logger.LogInformation("Rate Limit hit for Kavita+ prefetch");
            return false;
        }

        // Prefetch SeriesDetail data
        return await GetSeriesDetailPlus(seriesId, libraryType, trigger, ct) != null;
    }

    public async Task<IList<MalStackDto>> GetStacksForUser(int userId, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return ArraySegment<MalStackDto>.Empty;

        // See if this user has Mal account on record
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) return ArraySegment<MalStackDto>.Empty;

        var scrobbleSettings = user.ScrobbleProviders[ScrobbleProvider.Mal];

        if (string.IsNullOrEmpty(scrobbleSettings.UserName) || string.IsNullOrEmpty(scrobbleSettings.AuthenticationToken))
        {
            _logger.LogInformation("User is attempting to fetch MAL Stacks, but missing information on their account");
            return ArraySegment<MalStackDto>.Empty;
        }

        try
        {
            _logger.LogDebug("Fetching Kavita+ for MAL Stacks for user {UserName}", scrobbleSettings.UserName);

            var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            return await _kavitaPlusApiService.GetMalStacksAsync(scrobbleSettings.UserName, license, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fetching Kavita+ for MAL Stacks for user {UserName} failed", scrobbleSettings.UserName);
            return ArraySegment<MalStackDto>.Empty;
        }
    }

    public async Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId,
            SeriesIncludes.Metadata | SeriesIncludes.ExternalMetadata | SeriesIncludes.Library, ct);
        if (series == null) return [];

        var query = dto.Query;

        var potentialAnilistId = ExternalIdParser.TryParseAniListHeader(query, out var aniListId)
            ? aniListId : ExternalIdParser.GetAniListId(query);

        var potentialMalId = ExternalIdParser.TryParseMalHeader(query, out var malId)
            ? malId : ExternalIdParser.GetMalId(query);

        var potentialMangabakaId = ExternalIdParser.TryParseMangaBakaHeader(query, out var mangabakaId)
            ? mangabakaId : ExternalIdParser.GetMangaBakaId(query);

        var potentialHardcoverSlug = ExternalIdParser.TryParseHardcoverHeader(query, out var hardcoverId)
            ? hardcoverId : null;

        // TODO: Clean this logic up once we move to v3
        var potentialCbrSlug = query.Contains("comicbookroundup.com/") ? query : null;

        // If any ID was extracted (header syntax or URL), the raw query string is meaningless to the backend
        var wasHeaderQuery = potentialAnilistId.HasValue
                             || potentialMalId.HasValue
                             || potentialMangabakaId > 0
                             || !string.IsNullOrEmpty(potentialHardcoverSlug)
                             ;//|| !string.IsNullOrEmpty(potentialCbrSlug); // For now, we pass slug as query as there is a direct handling on Query currently

        query = wasHeaderQuery ? null : dto.Query;

        var format = series.Library.Type.ConvertToPlusMediaFormat(series.Format);
        var otherNames = ExtractAlternativeNames(series);

        var year = series.Metadata.ReleaseYear;
        if (year == 0 && format == PlusMediaFormat.Comic && !string.IsNullOrWhiteSpace(series.Name))
        {
            var potentialYear = Parser.ParseYear(series.Name);
            if (!string.IsNullOrEmpty(potentialYear))
            {
                year = int.Parse(potentialYear);
            }
        }

        // TODO: Match needs to be overhauled
        var matchRequest = new MatchSeriesRequestDto()
        {
            Format = format,
            Query = query,
            SeriesName = series.Name,
            AlternativeNames = otherNames,
            Year = year,
            AniListId = potentialAnilistId ?? ScrobblingHelper.GetAniListId(series), // TODO: Opportunity to streamline this with ExternalIdParser and the default > 0/empty string checks
            MalId = potentialMalId ?? ScrobblingHelper.GetMalId(series),
            MangabakaId = potentialMangabakaId > 0 ? (int) potentialMangabakaId : (int?) series.MangaBakaId,
            HardcoverSlug = potentialHardcoverSlug,
            CbrSlug = potentialCbrSlug
        };

        try
        {
            var results = await _kavitaPlusApiService.MatchSeriesAsync(matchRequest, ct);

            // Some summaries can contain multiple <br/>s, we need to ensure it's only 1
            foreach (var result in results)
            {
                result.Series.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(result.Series.Summary));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error happened during the request to Kavita+ API");
        }

        return ArraySegment<ExternalSeriesMatchDto>.Empty;
    }

    private static List<string> ExtractAlternativeNames(Series series)
    {
        List<string> altNames = [series.LocalizedName, series.OriginalName];
        return altNames.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
    }


    public async Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId, CancellationToken ct = default)
    {
        if (!aniListId.HasValue && !malId.HasValue)
        {
            throw new KavitaException("Unable to find valid information from url for External Load");
        }

        // This is for the Series drawer. We can get this extra information during the initial SeriesDetail call so it's all coming from the DB
        return await GetSeriesDetail(aniListId, malId, seriesId, ct);

    }

    public async Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default)
    {
        if (!IsPlusEligible(libraryType) || !await _licenseService.HasActiveLicense(ct: ct)) return _defaultReturn;

        // Check blacklist (bad matches) or if there is a don't match
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library,  ct: ct);
        if (series == null || !series.WillScrobble() || !series.Library.AllowMetadataMatching) return _defaultReturn;

        var needsRefresh =
            await _unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(seriesId, ct);

        if (!needsRefresh)
        {
            // Convert into DTOs and return
            return await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(seriesId, ct);
        }

        var data = await _unitOfWork.SeriesRepository.GetPlusSeriesDtoAsync(seriesId, ct);
        if (data == null) return _defaultReturn;

        // Get from Kavita+ API the Full Series metadata with rec/rev and cache to ExternalMetadata tables
        try
        {
            return await FetchExternalMetadataForSeries(seriesId, libraryType, data, false, trigger, ct);
        }
        catch (KavitaException ex)
        {
            _logger.LogError(ex, "Rate limit hit fetching metadata");
            // This can happen when we hit rate limit
            return _defaultReturn;
        }
    }

    public async Task FixSeriesMatch(int seriesId, ExternalMetadataIdsDto ids, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library, ct);
        if (series == null) return;

        // Remove from Blacklist
        series.IsBlacklisted = false;
        series.DontMatch = false;
        _unitOfWork.SeriesRepository.Update(series);
        _fileCacheService.InvalidatePrefix(GetCoversCacheKey(seriesId), FileCacheService.KavitaPlusCacheDirectory);

        // Refetch metadata with a Direct lookup
        try
        {
            var metadata = await FetchExternalMetadataForSeries(seriesId, series.Library.Type,
                new PlusSeriesRequestDto()
                {
                    AniListId = ids.AniListId,
                    MalId = ids.MalId,
                    CbrId = ids.CbrId,
                    MangabakaId = ids.MangabakaId,
                    HardcoverId = ids.HardcoverId,
                    MediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
                    SeriesName = series.Name // Required field, not used since provider Ids are passed
                }, true, MetadataFetchTrigger.ManualMatch, ct);

            if (metadata.Series == null)
            {
                _logger.LogError("Unable to Match {SeriesName} with Kavita+ Series with Ids: {AniListId}/{MalId}/{CbrId}/{MangabakaId}/{HardcoverId}",
                    series.Name, ids.AniListId, ids.MalId, ids.CbrId, ids.MangabakaId, ids.HardcoverId);
                return;
            }

            // Find all scrobble events and rewrite them to be the correct
            var events = await _unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId, ct);
            _unitOfWork.ScrobbleRepository.Remove(events);

            // Find all scrobble errors and remove them
            var errors = await _unitOfWork.ScrobbleRepository.GetAllScrobbleErrorsForSeries(seriesId, ct);
            _unitOfWork.ScrobbleRepository.Remove(errors);



            await _unitOfWork.CommitAsync(ct);

            // Regenerate all events for the series for all users
            BackgroundJob.Enqueue(() => _scrobblingService.CreateEventsFromExistingHistoryForSeries(seriesId, CancellationToken.None));

            await _eventHub.SendMessageAsync(MessageFactory.SeriesUpdated, MessageFactory.SeriesUpdatedEvent(series.Id), ct: ct);

            // Name can be null on Series even with a direct match
            _logger.LogInformation("Matched {SeriesName} with Kavita+ Series {MatchSeriesName}", series.Name,
                metadata.Series.Name);
            await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMatchFixed, seriesId,
                new AuditLogMatchClearedParamsDto { SeriesName = series.Name, MatchedName = metadata.Series.Name }, ct: ct);
        }
        catch (KavitaException ex)
        {
            // We can't rethrow because Fix match is done in a background thread and Hangfire will requeue multiple times
            _logger.LogInformation(ex, "Rate limit hit for matching {SeriesName} with Kavita+", series.Name);
            await _eventHub.SendMessageAsync(MessageFactory.ExternalMatchRateLimitError,
                MessageFactory.ExternalMatchRateLimitErrorEvent(series.Id, series.Name), ct: ct);
        }
    }

    public async Task UpdateSeriesDontMatch(int seriesId, bool dontMatch, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.ExternalMetadata, ct);
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
            _fileCacheService.InvalidatePrefix(GetCoversCacheKey(seriesId), FileCacheService.KavitaPlusCacheDirectory);
        }

        _unitOfWork.SeriesRepository.Update(series);

        await _unitOfWork.CommitAsync(ct);

        // Send a series Update to ensure pages get the new information
        await _eventHub.SendMessageAsync(MessageFactory.SeriesUpdated, MessageFactory.SeriesUpdatedEvent(series.Id), ct: ct);

        await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesDontMatchSet, seriesId,
            new AuditLogMatchDontMatchParamsDto { SeriesName = series.Name, DontMatch = dontMatch }, ct: ct);
    }

    /// <summary>
    /// Requests the full SeriesDetail (rec, review, metadata) data for a Series. Will save to ExternalMetadata tables.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="data"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task<SeriesDetailPlusDto> FetchExternalMetadataForSeries(int seriesId, LibraryType libraryType, PlusSeriesRequestDto data,
        bool fromMatchFlow = false, MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default)
    {

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library, ct);
        if (series == null)
        {
            return _defaultReturn;
        }

        try
        {
            _logger.LogDebug("Fetching Kavita+ Series Detail data for {SeriesName}", string.IsNullOrEmpty(data.SeriesName) ? data.AniListId : data.SeriesName);
            SeriesDetailPlusApiDto? result = null;

            await _auditService.LogAsync(
                KavitaPlusAuditCategory.Metadata,
                KavitaPlusEventType.MetadataFetched,
                AuditStatus.Info,
                AuditSubjectType.Series,
                seriesId: seriesId,
                payload: new AuditLogMetadataFetchParamsDto
                {
                    SeriesId = seriesId,
                    LibraryId = series.Library?.Id,
                    Format = series.Format,
                    MangaBakaId = series.MangaBakaId,
                    CbrId = series.CbrId,
                    AniListId = series.AniListId,
                    HardcoverId = series.HardcoverId,
                    Trigger = trigger,
                },
                ct: ct);

            try
            {
                // This returns an AniListSeries and Match returns ExternalSeriesDto
                result = await _kavitaPlusApiService.GetSeriesDetailAsync(data, ct);

            }
            catch (FlurlHttpException ex)
            {
                var errorMessage = await ex.GetResponseStringAsync() ?? string.Empty;
                // Trim quotes if the response is a JSON string
                errorMessage = errorMessage.Trim('"');

                if (ex.StatusCode == 400)
                {
                    if (errorMessage.Contains("Too many Requests"))
                    {
                        _logger.LogDebug("Hit rate limit, will retry in 3 seconds");
                        await Task.Delay(3000, ct);

                        result = await _kavitaPlusApiService.GetSeriesDetailAsync(data, ct);
                    }
                    else if (errorMessage.Contains("Unknown Series"))
                    {
                        series.IsBlacklisted = true;
                        await _unitOfWork.CommitAsync(ct);
                        await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesBlacklisted, seriesId,
                            new AuditLogMatchFailureParamsDto { SeriesName = series.Name, Reason = "unknown-series" }, AuditStatus.Failure, ct: ct);
                    }
                }
            }

            if (result == null)
            {
                _logger.LogInformation("Hit rate limit twice, try again later");
                await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMatchFailed, seriesId,
                    new AuditLogMatchFailureParamsDto { SeriesName = series.Name, Reason = "rate-limit-hit" }, AuditStatus.Failure, ct: ct);
                return _defaultReturn;
            }

            // Clear out existing results
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalReviews);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRatings);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRecommendations);

            // TODO: Do not hardcode - Metadata Rework
            externalSeriesMetadata.Provider = result.MangabakaId > 0 ? MetadataProvider.Mangabaka :
                    (result.CbrId > 0 ? MetadataProvider.ComicBookRoundup : MetadataProvider.Hardcover);

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
                rating.ProviderUrl = r.ProviderUrl;
                return rating;
            }).ToList();


            // Recommendations
            externalSeriesMetadata.ExternalRecommendations ??= [];
            var recs = await ProcessRecommendations(libraryType, result.Recommendations, externalSeriesMetadata);

            var extRatings = externalSeriesMetadata.ExternalRatings
                .Where(r => r.AverageScore > 0)
                .ToList();

            externalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.Add(_externalSeriesMetadataCache);
            externalSeriesMetadata.AverageExternalRating = extRatings.Count != 0 ? (int) extRatings
                .Average(r => r.AverageScore) : 0;

            // prefer what was passed in (manual match), fall back to what K+ returned
            var beforeIds = new AuditLogMatchExternalIdsParamsDto { AniListId = series.AniListId, MalId = series.MalId, MangaBakaId = series.MangaBakaId, CbrId = series.CbrId, HardcoverId = series.HardcoverId };

            // TODO: Need to rethink how all these Ids work and only write on MetadataFetchTrigger.OnDemand
            externalSeriesMetadata.MalId = data.MalId ?? result.MalId ?? 0;
            externalSeriesMetadata.AniListId = data.AniListId ?? result.AniListId ?? 0;
            externalSeriesMetadata.CbrId = data.CbrId ?? result.CbrId ?? 0;
            externalSeriesMetadata.MangabakaId = data.MangabakaId ?? result.MangabakaId ?? 0;
            series.MangaBakaId = externalSeriesMetadata.MangabakaId;
            var hardcoverId = data.HardcoverId ?? result.Series?.HardcoverId ?? series.HardcoverId;
            var afterIds = new AuditLogMatchExternalIdsParamsDto {
                AniListId = externalSeriesMetadata.AniListId,
                MalId = externalSeriesMetadata.MalId,
                MangaBakaId = series.MangaBakaId,
                CbrId = externalSeriesMetadata.CbrId,
                HardcoverId = hardcoverId };

            await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMatched, seriesId,
                new AuditLogMatchedParamsDto {
                    SeriesName = series.Name,
                    Before = beforeIds, After = afterIds,
                    MatchedName = result.Series?.Name
                }, ct: ct);

            // If there is metadata and the user has metadata download turned on
            var madeMetadataModification = false;
            if (result.Series != null && (series.Library!.AllowMetadataMatching || fromMatchFlow))
            {
                externalSeriesMetadata.Series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ct: ct);

                try
                {
                    madeMetadataModification = await WriteExternalMetadataToSeries(result.Series, seriesId, trigger, ct);
                    if (madeMetadataModification)
                    {
                        _unitOfWork.SeriesRepository.Update(series);
                        _unitOfWork.SeriesRepository.Update(series.Metadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was an exception when trying to write Series metadata from Kavita+");
                }

            }

            // WriteExternalMetadataToSeries will commit but not always
            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync(ct);
            }

            if (madeMetadataModification)
            {
                // Inform the UI of the update
                await _eventHub.SendMessageAsync(MessageFactory.ScanSeries, MessageFactory.ScanSeriesEvent(series.LibraryId, series.Id, series.Name), false, ct);
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
            var errorMessage = await ex.GetResponseStringAsync();
            // Trim quotes if the response is a JSON string
            errorMessage = errorMessage.Trim('"');

            if (ex.StatusCode == 500)
            {
                return _defaultReturn;
            }

            if (ex.StatusCode == 400 && errorMessage.Contains("Too many Requests"))
            {
                throw new KavitaException("Too many requests, slow down");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Too Many Requests"))
            {
                throw new KavitaException("Too many requests, slow down");
            }

            _logger.LogError(ex, "Unable to fetch external series metadata from Kavita+");
        }

        // Blacklist the series as it wasn't found in Kavita+
        series.IsBlacklisted = true;
        await _unitOfWork.CommitAsync(ct);

        return _defaultReturn;
    }

    public async Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId, MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default)
    {
        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto(ct);
        if (!settings.Enabled) return false;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Related, ct);
        if (series == null) return false;

        var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct);

        _logger.LogInformation("Writing External metadata to Series {SeriesName}", series.Name);

        var madeModification = false;
        var fieldChanges = new List<MetadataFieldChangeDto>();
        var processedGenres = new List<string>();
        var processedTags = new List<string>();

        // TODO: Clean this up with a helper
        Accumulate(ref madeModification, fieldChanges, UpdateSummary(series, settings, externalMetadata));
        Accumulate(ref madeModification, fieldChanges, UpdateReleaseYear(series, settings, externalMetadata));
        Accumulate(ref madeModification, fieldChanges, UpdateLocalizedName(series, settings, externalMetadata));
        Accumulate(ref madeModification, fieldChanges, await UpdatePublicationStatus(series, settings, externalMetadata));
        if (trigger == MetadataFetchTrigger.OnDemand)
        {
            Accumulate(ref madeModification, fieldChanges, UpdateExternalIds(series, externalMetadata));
        }

        // Apply field mappings
        GenerateGenreAndTagLists(externalMetadata, settings, ref processedTags, ref processedGenres);

        Accumulate(ref madeModification, fieldChanges, await UpdateGenres(series, settings, externalMetadata, processedGenres));
        Accumulate(ref madeModification, fieldChanges, await UpdateTags(series, settings, externalMetadata, processedTags));
        Accumulate(ref madeModification, fieldChanges, UpdateAgeRating(series, settings, processedGenres.Concat(processedTags)));

        var staff = await SetNameAndAddAliases(settings, externalMetadata.Staff);

        Accumulate(ref madeModification, fieldChanges, await UpdateWriters(series, settings, staff));
        Accumulate(ref madeModification, fieldChanges, await UpdateArtists(series, settings, staff));
        Accumulate(ref madeModification, fieldChanges, await UpdateCharacters(series, settings, externalMetadata.Characters));

        Accumulate(ref madeModification, fieldChanges, await UpdateRelationships(series, settings, externalMetadata.Relations, defaultAdmin));
        try
        {
            madeModification = await UpdateCoverImage(series, settings, externalMetadata) || madeModification;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch cover image");
        }

        madeModification = await UpdateChapters(series, settings, externalMetadata) || madeModification;

        if (fieldChanges.Count > 0)
        {
            await _auditService.LogMetadataAsync(seriesId, fieldChanges, ct);
        }

        return madeModification;
    }

    public async Task<IList<ExternalCoverResponseDto>> GetExternalCovers(int seriesId, int? volumeId = null, int? chapterId = null, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Chapters, ct: ct);
        if (series == null) throw new KavitaException("Series not found");

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId, ct);

        var payload = new ExternalCoverRequestDto()
        {
            SeriesName = series.Name,
            AltSeriesName = series.LocalizedName,
            MediaFormat = libraryType.ConvertToPlusMediaFormat(),
            AniListId = series.AniListId,
            ComicVineId = series.ComicVineId,
            HardcoverId = series.HardcoverId,
            MangabakaId = (int) series.MangaBakaId,
            MalId = series.MalId,
            MetronId = series.MetronId,
            CbrId = series.CbrId,
            IsStandAlone = series.Volumes.Sum(v => v.Chapters.Count) == 1, // TODO: Temp code, update to series field
        };

        if (volumeId.HasValue)
        {
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId.Value, ct: ct);
            if (volume == null) throw new KavitaException("Volume not found");
            payload.VolumeNumber = volume.MinNumber;
            payload.VolumesOnly = true;
        }

        if (chapterId.HasValue)
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId.Value, 0, ct: ct);
            if (chapter == null) throw new KavitaException("Chapter not found");
            payload.ChapterNumber = chapter.MinNumber;
            payload.ChaptersOnly = true;
            payload.VolumesOnly = false;
        }

        var cacheKey = GetCoversCacheKey(seriesId, volumeId, chapterId);

        var result = await _fileCacheService.GetOrFetchAsync<KPlusResult<IList<ExternalCoverResponseDto>>>(
            cacheKey,
            FileCacheService.KavitaPlusCacheDirectory,
            TimeSpan.FromDays(7),
            async _ => await _kavitaPlusApiService.GetCoverImagesAsync(payload, ct),
            shouldCache: r => r?.IsSuccess == true,
            ct: ct);

        if (result is null || !result.IsSuccess)
        {
            _logger.LogWarning("[Covers] Failed to retrieve covers for Series {SeriesId}: {Error}",
                seriesId, result?.ErrorMessage);
            return [];
        }

        return result.Data ?? [];
    }

    private static string GetCoversCacheKey(int seriesId, int? volumeId = null, int? chapterId = null)
    {
        var chapterPart = chapterId.HasValue ? $"-chp-{chapterId}" : string.Empty;
        var volumePart = volumeId.HasValue ? $"-vol-{volumeId}" : string.Empty;

        return $"covers-series-{seriesId}{volumePart}{chapterPart}";
    }

    private async Task<List<SeriesStaffDto>> SetNameAndAddAliases(MetadataSettingsDto settings, IList<SeriesStaffDto>? staff)
    {
        if (staff == null || staff.Count == 0) return [];

        var nameMappings = staff.Select(s => new
        {
            Staff = s,
            PreferredName = settings.FirstLastPeopleNaming ? $"{s.FirstName} {s.LastName}" : $"{s.LastName} {s.FirstName}",
            AlternativeName = !settings.FirstLastPeopleNaming ? $"{s.FirstName} {s.LastName}" : $"{s.LastName} {s.FirstName}"
        }).ToList();

        var preferredNames = nameMappings.Select(n => n.PreferredName.ToNormalized()).Distinct().ToList();
        var alternativeNames = nameMappings.Select(n => n.AlternativeName.ToNormalized()).Distinct().ToList();

        var existingPeople = await _unitOfWork.PersonRepository.GetPeopleByNames(preferredNames.Union(alternativeNames).ToList());
        var existingPeopleDictionary = PersonHelper.ConstructNameAndAliasDictionary(existingPeople);

        var modified = false;
        foreach (var mapping in nameMappings)
        {
            mapping.Staff.Name = mapping.PreferredName;

            if (existingPeopleDictionary.ContainsKey(mapping.PreferredName.ToNormalized()))
            {
                continue;
            }


            if (existingPeopleDictionary.TryGetValue(mapping.AlternativeName.ToNormalized(), out var person))
            {
                modified = true;
                person.Aliases.Add(new PersonAliasBuilder(mapping.PreferredName).Build());
                await _auditService.LogPersonAsync(KavitaPlusEventType.PersonAliasAdded, person.Id,
                    new AuditLogPersonAliasParamsDto { PersonName = person.Name, AliasAdded = mapping.PreferredName });
            }
        }

        if (modified)
        {
            await _unitOfWork.CommitAsync();
        }

        return [.. staff];
    }

    /// <summary>
    /// Helper method, calls <see cref="GenerateGenreAndTagLists"/>
    /// </summary>
    /// <param name="externalMetadata"></param>
    /// <param name="settings"></param>
    /// <param name="processedTags"></param>
    /// <param name="processedGenres"></param>
    private static void GenerateGenreAndTagLists(ExternalSeriesDetailDto externalMetadata, MetadataSettingsDto settings,
        ref List<string> processedTags, ref List<string> processedGenres)
    {
        externalMetadata.Tags ??= [];
        externalMetadata.Genres ??= [];
        GenerateGenreAndTagLists(externalMetadata.Genres, externalMetadata.Tags.Select(t => t.Name).ToList(),
            settings, ref processedTags, ref processedGenres);
    }

    /// <summary>
    /// Run all genres and tags through the Metadata settings
    /// </summary>
    /// <param name="genres">Genres to process</param>
    /// <param name="tags">Tags to process</param>
    /// <param name="settings"></param>
    /// <param name="processedTags"></param>
    /// <param name="processedGenres"></param>
    private static void GenerateGenreAndTagLists(IList<string> genres, IList<string> tags, MetadataSettingsDto settings,
        ref List<string> processedTags, ref List<string> processedGenres)
    {
        var mappings = ApplyFieldMappings(tags, MetadataFieldType.Tag, settings.FieldMappings);
        if (mappings.TryGetValue(MetadataFieldType.Tag, out var tagsToTags))
        {
            processedTags.AddRange(tagsToTags);
        }
        if (mappings.TryGetValue(MetadataFieldType.Genre, out var tagsToGenres))
        {
            processedGenres.AddRange(tagsToGenres);
        }

        mappings = ApplyFieldMappings(genres, MetadataFieldType.Genre, settings.FieldMappings);
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

    /// <summary>
    /// Processes the given tags and genres only if <see cref="MetadataSettingsDto.EnableExtendedMetadataProcessing"/>
    /// is true, else return without change
    /// </summary>
    /// <param name="genres"></param>
    /// <param name="tags"></param>
    /// <param name="settings"></param>
    /// <param name="processedTags"></param>
    /// <param name="processedGenres"></param>
    public static void GenerateExternalGenreAndTagsList(IList<string> genres, IList<string> tags,
        MetadataSettingsDto settings, out List<string> processedTags, out List<string> processedGenres)
    {
        if (!settings.EnableExtendedMetadataProcessing)
        {
            processedTags = [..tags];
            processedGenres = [..genres];
            return;
        }

        processedTags = [];
        processedGenres = [];
        GenerateGenreAndTagLists(genres, tags, settings, ref processedTags, ref processedGenres);
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateRelationships(Series series, MetadataSettingsDto settings, IList<SeriesRelationship>? externalMetadataRelations, AppUser defaultAdmin)
    {
        if (!settings.EnableRelationships) return (false, null);

        if (externalMetadataRelations == null || externalMetadataRelations.Count == 0 || defaultAdmin == null)
        {
            return (false, null);
        }

        var addedRelations = new List<object>();
        foreach (var relation in externalMetadataRelations.Where(r => r.Relation != RelationKind.Parent))
        {
            List<string> names = new [] {relation.SeriesName.PreferredTitle, relation.SeriesName.RomajiTitle, relation.SeriesName.EnglishTitle, relation.SeriesName.NativeTitle}.Where(s => !string.IsNullOrEmpty(s)).ToList()!;
            var relatedSeries = await _unitOfWork.SeriesRepository.GetSeriesByAnyNameAsync(
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
            addedRelations.Add(new { relatedSeriesName = relatedSeries.Name, relatedSeriesId = relatedSeries.Id, kind = relation.Relation.ToString() });

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

        if (!_unitOfWork.HasChanges()) return (false, null);
        await _unitOfWork.CommitAsync();

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Relationships, null, addedRelations));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateCharacters(Series series, MetadataSettingsDto settings, IList<SeriesCharacter>? externalCharacters)
    {
        if (!settings.EnablePeople) return (false, null);

        if (externalCharacters == null || externalCharacters.Count == 0) return (false, null);

        if (series.Metadata.CharacterLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.People))
        {
            return (false, null);
        }

        if (!settings.IsPersonAllowed(PersonRole.Character))
        {
            return (false, null);
        }

        series.Metadata.People ??= [];

        var characters = externalCharacters
            .Select(w => new PersonDto()
            {
                Name = w.Name.Trim(),
                AniListId = ExternalIdParser.GetAniListCharacterId(w.Url),
                Description = StringHelper.CorrectUrls(StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(w.Description))),
            })
            .Concat(series.Metadata.People
                .Where(p => p.Role == PersonRole.Character)
                // Need to ensure existing people are retained, but we overwrite anything from a bad match
                .Where(p => !p.KavitaPlusConnection)
                .Select(p => _mapper.Map<PersonDto>(p.Person))
            )
            .DistinctBy(p => Parser.Normalize(p.Name))
            .ToList();

        if (characters.Count == 0) return (false, null);

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
            var aniListId = ExternalIdParser.GetAniListCharacterId(character.Url);
            if (aniListId <= 0) continue;
            var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId);
            if (person != null && !string.IsNullOrEmpty(character.ImageUrl) && string.IsNullOrEmpty(person.CoverImage))
            {
                await _coverDbService.SetPersonCoverByUrl(person, character.ImageUrl, false);
            }
        }

        series.Metadata.AddKPlusOverride(MetadataSettingField.People);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Characters, null, externalCharacters.Select(c => c.Name).ToList()));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateArtists(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return (false, null);

        var upstreamArtists = staff
            .Where(s => s.Role is "Art" or "Story & Art")
            .ToList();

        if (upstreamArtists.Count == 0) return (false, null);

        if (series.Metadata.CoverArtistLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.People))
        {
            return (false, null);
        }

        if (!settings.IsPersonAllowed(PersonRole.CoverArtist))
        {
            return (false, null);
        }

        series.Metadata.People ??= [];
        var artists = upstreamArtists
            .Select(w => new PersonDto()
            {
                Name = w.Name.Trim(),
                AniListId = ExternalIdParser.GetAniListStaffId(w.Url),
                Description = StringHelper.CorrectUrls(StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(w.Description))),
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

        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync();

        await DownloadAndSetPersonCovers(upstreamArtists);
        series.Metadata.AddKPlusOverride(MetadataSettingField.People);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Artists, null, upstreamArtists.Select(a => a.Name).ToList()));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateWriters(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return (false, null);

        var upstreamWriters = staff
            .Where(s => s.Role is "Story" or "Story & Art")
            .ToList();

        if (upstreamWriters.Count == 0) return (false, null);

        if (series.Metadata.WriterLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.People))
        {
            return (false, null);
        }

        if (!settings.IsPersonAllowed(PersonRole.Writer))
        {
            return (false, null);
        }

        series.Metadata.People ??= [];
        var writers = upstreamWriters
            .Select(w => new PersonDto()
            {
                Name = w.Name.Trim(),
                AniListId = ExternalIdParser.GetAniListStaffId(w.Url),
                Description = StringHelper.CorrectUrls(StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(w.Description))),
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
        series.Metadata.AddKPlusOverride(MetadataSettingField.People);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Writers, null, upstreamWriters.Select(w => w.Name).ToList()));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateTags(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata, List<string> processedTags)
    {
        externalMetadata.Tags ??= [];

        if (!settings.EnableTags || processedTags.Count == 0) return (false, null);

        if (series.Metadata.TagsLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Tags))
        {
            return (false, null);
        }

        _logger.LogDebug("Found {TagCount} tags for {SeriesName}", processedTags.Count, series.Name);
        var madeModification = false;
        series.Metadata.Tags ??= [];
        var before = series.Metadata.Tags.Select(t => t.Title).ToList();
        var allTags = (await _unitOfWork.TagRepository.GetAllTagsByNameAsync(processedTags.Select(Parser.Normalize)))
            .ToList();

        TagHelper.UpdateTagList(processedTags, series.Metadata.Tags, allTags, tag =>
        {
            series.Metadata.Tags.Add(tag);
            madeModification = true;
        }, () => series.Metadata.TagsLocked = true);

        if (!madeModification) return (false, null);
        series.Metadata.AddKPlusOverride(MetadataSettingField.Tags);
        var after = series.Metadata.Tags.Select(t => t.Title).ToList();

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Tags, before, after));
    }

    private static List<string> ApplyBlackWhiteList(MetadataSettingsDto settings, MetadataFieldType fieldType, List<string> processedStrings)
    {
        var whiteList = settings.Whitelist.Select(t => t.ToNormalized()).ToList();
        var blackList = settings.Blacklist.Select(t => t.ToNormalized()).ToList();

        return fieldType switch
        {
            MetadataFieldType.Genre => processedStrings.Distinct()
                .Where(g => blackList.Count == 0 || !blackList.Contains(g.ToNormalized()))
                .ToList(),
            MetadataFieldType.Tag => processedStrings.Distinct()
                .Where(g => blackList.Count == 0 || !blackList.Contains(g.ToNormalized()))
                .Where(g => whiteList.Count == 0 || whiteList.Contains(g.ToNormalized()))
                .ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(fieldType), fieldType, null),
        };
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateGenres(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata, List<string> processedGenres)
    {
        externalMetadata.Genres ??= [];

        if (!settings.EnableGenres || processedGenres.Count == 0) return (false, null);

        if (series.Metadata.GenresLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Genres))
        {
            return (false, null);
        }

        _logger.LogDebug("Found {GenreCount} genres for {SeriesName}", processedGenres.Count, series.Name);
        var madeModification = false;
        series.Metadata.Genres ??= [];
        var before = series.Metadata.Genres.Select(g => g.Title).ToList();
        var existingGenres = series.Metadata.Genres;
        var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(processedGenres.Select(Parser.Normalize))).ToList();

        TagHelper.UpdateTagList(processedGenres, series.Metadata.Genres, allGenres, genre =>
        {
            series.Metadata.Genres.Add(genre);
            madeModification = true;
        }, () => series.Metadata.GenresLocked = true);

        foreach (var genre in existingGenres)
        {
            if (series.Metadata.Genres.FirstOrDefault(g => g.NormalizedTitle == genre.NormalizedTitle) != null) continue;
            series.Metadata.Genres.Add(genre);
            madeModification = true;
        }

        if (!madeModification) return (false, null);
        series.Metadata.AddKPlusOverride(MetadataSettingField.Genres);
        var after = series.Metadata.Genres.Select(g => g.Title).ToList();

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Genres, before, after));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdatePublicationStatus(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnablePublicationStatus) return (false, null);

        if (series.Metadata.PublicationStatusLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.PublicationStatus))
        {
            return (false, null);
        }

        try
        {
            var from = series.Metadata.PublicationStatus;
            var chapters =
                (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(series.Id, SeriesIncludes.Chapters))!.Volumes
                .SelectMany(v => v.Chapters).ToList();
            var status = DeterminePublicationStatus(series, chapters, externalMetadata);

            series.Metadata.PublicationStatus = status;
            series.Metadata.PublicationStatusLocked = true;
            series.Metadata.AddKPlusOverride(MetadataSettingField.PublicationStatus);

            return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.PublicationStatus, from.ToString(), status.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue determining Publication Status for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }

        return (false, null);
    }

    private (bool, MetadataFieldChangeDto?) UpdateAgeRating(Series series, MetadataSettingsDto settings, IEnumerable<string> allExternalTags)
    {
        if (series.Metadata.AgeRatingLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.AgeRating))
        {
            return (false, null);
        }

        try
        {
            var totalTags = allExternalTags
                .Concat(series.Metadata.Genres.Select(g => g.Title))
                .Concat(series.Metadata.Tags.Select(g => g.Title));

            var from = series.Metadata.AgeRating;
            var ageRating = DetermineAgeRating(totalTags, settings.AgeRatingMappings);
            if (series.Metadata.AgeRating <= ageRating)
            {
                series.Metadata.AgeRating = ageRating;
                series.Metadata.AddKPlusOverride(MetadataSettingField.AgeRating);

                return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.AgeRating, from.ToString(), ageRating.ToString()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue determining Age Rating for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }

        return (false, null);
    }

    private static (bool, MetadataFieldChangeDto?) UpdateExternalIds(Series series, ExternalSeriesDetailDto externalMetadata)
    {
        var madeModification = false;
        var from = new { aniListId = series.AniListId, malId = series.MalId, cbrId = series.CbrId, mangaBakaId = series.MangaBakaId, hardcoverId = series.HardcoverId };
        if (externalMetadata.AniListId is > 0)
        {
            series.AniListId = externalMetadata.AniListId.Value;
            madeModification = true;
        }

        if (externalMetadata.MALId is > 0)
        {
            series.MalId = externalMetadata.MALId.Value;
            madeModification = true;
        }

        if (externalMetadata.CbrId is > 0)
        {
            series.CbrId = externalMetadata.CbrId.Value;
            madeModification = true;
        }

        if (externalMetadata.MangabakaId is > 0)
        {
            series.MangaBakaId = externalMetadata.MangabakaId.Value;
            madeModification = true;
        }

        if (externalMetadata.HardcoverId is > 0)
        {
            series.HardcoverId = externalMetadata.HardcoverId.Value;
            madeModification = true;
        }

        // Add the rest of the Ids (Metron/ComicVine) when Kavita+ has them

        if (!madeModification) return (false, null);
        var to = new { aniListId = series.AniListId, malId = series.MalId, cbrId = series.CbrId, mangaBakaId = series.MangaBakaId, hardcoverId = series.HardcoverId };

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.ExternalIds, from, to));
    }


    private async Task<bool> UpdateChapters(Series series, MetadataSettingsDto settings,
        ExternalSeriesDetailDto externalMetadata)
    {
        if (externalMetadata.PlusMediaFormat != PlusMediaFormat.Comic) return false;

        if (externalMetadata.ChapterDtos == null || externalMetadata.ChapterDtos.Count == 0) return false;

        // Get all volumes and chapters
        var madeModification = false;
        var allChapters =  await _unitOfWork.ChapterRepository.GetAllChaptersForSeries(series.Id);

        var matchedChapters = allChapters
            .Join(
                externalMetadata.ChapterDtos,
                chapter => chapter.Range,
                dto => dto.IssueNumber,
                (chapter, dto) => (chapter, dto)
            )
            .ToList();

        foreach (var (chapter, potentialMatch) in matchedChapters)
        {
            _logger.LogDebug("Updating {SeriesName} ({SeriesId}) - Chapter {ChapterNumber} with metadata", series.Name, series.Id, chapter.Range);
            var chapterFieldChanges = new List<MetadataFieldChangeDto>();

            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterTitle(chapter, settings, potentialMatch.Title, series.Name));
            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterSummary(chapter, settings, potentialMatch.Summary));
            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterReleaseDate(chapter, settings, potentialMatch.ReleaseDate));

            var hasUpdatedPublisher = await UpdateChapterPublisher(chapter, settings, potentialMatch.Publisher);
            if (hasUpdatedPublisher) chapter.AddKPlusOverride(MetadataSettingField.ChapterPublisher);
            madeModification = hasUpdatedPublisher || madeModification;

            madeModification = await UpdateChapterPeople(chapter, settings, PersonRole.CoverArtist, potentialMatch.Artists) || madeModification;
            madeModification = await UpdateChapterPeople(chapter, settings, PersonRole.Writer, potentialMatch.Writers) || madeModification;

            madeModification = await UpdateChapterCoverImage(chapter, settings, series.Id, potentialMatch.CoverImageUrl) || madeModification;
            madeModification = await UpdateExternalChapterMetadata(chapter, settings, potentialMatch) || madeModification;

            if (chapterFieldChanges.Count > 0)
            {
                await _auditService.LogChapterMetadataAsync(chapter.Id, series.Id, chapterFieldChanges);
            }

            _unitOfWork.ChapterRepository.Update(chapter);
            await _unitOfWork.CommitAsync();
        }

        return madeModification;
    }

    private async Task<bool> UpdateExternalChapterMetadata(Chapter chapter, MetadataSettingsDto settings, ExternalChapterDto metadata)
    {
        if (!settings.Enabled) return false;

        if (metadata.UserReviews.Count == 0 && metadata.CriticReviews.Count == 0)
        {
            return false;
        }

        var madeModification = false;

        #region Review

        // Remove existing Reviews
        var existingReviews = await _unitOfWork.ChapterRepository.GetExternalChapterReview(chapter.Id);
        _unitOfWork.ExternalSeriesMetadataRepository.Remove(existingReviews);


        List<ExternalReview> externalReviews = [];
        externalReviews.AddRange(metadata.CriticReviews
            .Where(r => !string.IsNullOrWhiteSpace(r.Username) && !string.IsNullOrWhiteSpace(r.Body))
            .Select(r =>
            {
                var review = _mapper.Map<ExternalReview>(r);
                review.ChapterId = chapter.Id;
                review.Authority = RatingAuthority.Critic;
                CleanCbrReview(ref review);
                return review;
            }));
        externalReviews.AddRange(metadata.UserReviews
            .Where(r => !string.IsNullOrWhiteSpace(r.Username) && !string.IsNullOrWhiteSpace(r.Body))
            .Select(r =>
            {
                var review = _mapper.Map<ExternalReview>(r);
                review.ChapterId = chapter.Id;
                review.Authority = RatingAuthority.User;
                CleanCbrReview(ref review);
                return review;
            }));

        chapter.ExternalReviews = externalReviews;
        madeModification = externalReviews.Count > 0;
        _logger.LogDebug("Added {Count} reviews for chapter {ChapterId}", externalReviews.Count, chapter.Id);
        #endregion

        #region Rating

        // C# can't make the implicit conversation here
        float? averageCriticRating = metadata.CriticReviews.Count > 0 ? metadata.CriticReviews.Average(r => r.Rating) : null;
        float? averageUserRating = metadata.UserReviews.Count > 0 ? metadata.UserReviews.Average(r => r.Rating) : null;

        var existingRatings = await _unitOfWork.ChapterRepository.GetExternalChapterRatings(chapter.Id);
        _unitOfWork.ExternalSeriesMetadataRepository.Remove(existingRatings);

        chapter.ExternalRatings = [];

        if (averageUserRating != null)
        {
            chapter.ExternalRatings.Add(new ExternalRating
            {
                AverageScore = (int) averageUserRating,
                Provider = ScrobbleProvider.Cbr,
                Authority = RatingAuthority.User,
                ProviderUrl = metadata.IssueUrl,

            });
            chapter.AverageExternalRating = averageUserRating.Value;
        }

        if (averageCriticRating != null)
        {
            chapter.ExternalRatings.Add(new ExternalRating
            {
                AverageScore = (int) averageCriticRating,
                Provider = ScrobbleProvider.Cbr,
                Authority = RatingAuthority.Critic,
                ProviderUrl = metadata.IssueUrl,

            });
        }

        madeModification = averageUserRating > 0f || averageCriticRating > 0f || madeModification;

        #endregion

        return madeModification;
    }

    private static void CleanCbrReview(ref ExternalReview review)
    {
        // CBR has Read Full Review which links to site, but we already have that
        review.Body = review.Body.Replace("Read Full Review", string.Empty).TrimEnd();
        review.RawBody = review.RawBody.Replace("Read Full Review", string.Empty).TrimEnd();
        review.BodyJustText = review.BodyJustText.Replace("Read Full Review", string.Empty).TrimEnd();
    }


    private static (bool, MetadataFieldChangeDto?) UpdateChapterSummary(Chapter chapter, MetadataSettingsDto settings, string? summary)
    {
        if (!settings.EnableChapterSummary) return (false, null);

        if (string.IsNullOrEmpty(summary)) return (false, null);

        if (chapter.SummaryLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterSummary))
        {
            return (false, null);
        }

        if (!string.IsNullOrWhiteSpace(summary) && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterSummary))
        {
            return (false, null);
        }

        var from = chapter.Summary;
        chapter.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(summary));
        chapter.AddKPlusOverride(MetadataSettingField.ChapterSummary);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Summary, from, chapter.Summary));
    }

    private static (bool, MetadataFieldChangeDto?) UpdateChapterTitle(Chapter chapter, MetadataSettingsDto settings, string? title, string seriesName)
    {
        if (!settings.EnableChapterTitle) return (false, null);

        if (string.IsNullOrEmpty(title)) return (false, null);

        if (chapter.TitleNameLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterTitle))
        {
            return (false, null);
        }

        if (!title.Contains(seriesName) && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterTitle))
        {
            return (false, null);
        }

        var from = chapter.TitleName;
        chapter.TitleName = title;
        chapter.AddKPlusOverride(MetadataSettingField.ChapterTitle);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Title, from, title));
    }

    private static (bool, MetadataFieldChangeDto?) UpdateChapterReleaseDate(Chapter chapter, MetadataSettingsDto settings, DateTime? releaseDate)
    {
        if (!settings.EnableChapterReleaseDate) return (false, null);

        if (releaseDate == null || releaseDate == DateTime.MinValue) return (false, null);

        if (chapter.ReleaseDateLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterReleaseDate))
        {
            return (false, null);
        }

        if (!HasForceOverride(settings, chapter, MetadataSettingField.ChapterReleaseDate))
        {
            return (false, null);
        }

        var from = chapter.ReleaseDate;
        chapter.ReleaseDate = releaseDate.Value;
        chapter.AddKPlusOverride(MetadataSettingField.ChapterReleaseDate);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.ReleaseDate, from, releaseDate.Value));
    }

    private async Task<bool> UpdateChapterPublisher(Chapter chapter, MetadataSettingsDto settings, string? publisher)
    {
        if (!settings.EnableChapterPublisher) return false;

        if (string.IsNullOrEmpty(publisher)) return false;

        if (chapter.PublisherLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterPublisher))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(publisher) && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterPublisher))
        {
            return false;
        }

        // Some publishers (CBR) can be represented as Boom! Studios/Boom! Town imprint, so let's handle that appropriately
        if (publisher.Contains('/') || publisher.Contains("imprint", StringComparison.InvariantCultureIgnoreCase))
        {
            var imprint = publisher.Split('/')[1].Replace("imprint", string.Empty);
            return await UpdateChapterPeople(chapter, settings, PersonRole.Publisher, [publisher]) ||
                await UpdateChapterPeople(chapter, settings, PersonRole.Imprint, [imprint]);
        }

        return await UpdateChapterPeople(chapter, settings, PersonRole.Publisher, [publisher]);
    }

    private async Task<bool> UpdateChapterCoverImage(Chapter chapter, MetadataSettingsDto settings, int seriesId, string? coverUrl)
    {
        if (!settings.EnableChapterCoverImage) return false;

        if (string.IsNullOrEmpty(coverUrl)) return false;

        if (chapter.CoverImageLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterCovers))
        {
            _logger.LogDebug("Kavita+ Update Chapter was skipped as cover was locked, Chapter: {ChapterId}", chapter.Id);
            return false;
        }

        await DownloadChapterCovers(chapter, coverUrl);
        chapter.AddKPlusOverride(MetadataSettingField.ChapterCovers);
        await _auditService.LogAsync(KavitaPlusAuditCategory.Metadata, KavitaPlusEventType.ChapterCoverUpdated, AuditStatus.Success,
            AuditSubjectType.Chapter, seriesId: seriesId, subjectId: chapter.Id,
            payload: new AuditLogChapterCoverParamsDto { IssueNumber = chapter.Range, CoverUrl = coverUrl });

        return true;
    }

    private async Task<bool> UpdateChapterPeople(Chapter chapter, MetadataSettingsDto settings, PersonRole role, IList<string>? staff)
    {
        if (!settings.EnablePeople) return false;

        if (staff?.Count == 0) return false;

        if (chapter.IsPersonRoleLocked(role) && !HasForceOverride(settings, chapter, MetadataSettingField.People))
        {
            return false;
        }

        if (!settings.IsPersonAllowed(role) && role != PersonRole.Publisher)
        {
            return false;
        }

        chapter.People ??= [];
        var people = staff!
            .Select(w => new PersonDto()
            {
                Name = w.Trim(),
            })
            .Concat(chapter.People
                .Where(p => p.Role == role)
                .Where(p => !p.KavitaPlusConnection)
                .Select(p => _mapper.Map<PersonDto>(p.Person))
            )
            .DistinctBy(p => Parser.Normalize(p.Name))
            .ToList();

        await PersonHelper.UpdateChapterPeopleAsync(chapter, staff ?? [], role, _unitOfWork);

        foreach (var person in chapter.People.Where(p => p.Role == role))
        {
            var meta = people.FirstOrDefault(c => c.Name == person.Person.Name);
            person.OrderWeight = 0;

            if (meta != null)
            {
                person.KavitaPlusConnection = true;
            }
        }

        _unitOfWork.ChapterRepository.Update(chapter);
        await _unitOfWork.CommitAsync();

        return true;
    }

    private async Task<bool> UpdateCoverImage(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableCoverImage) return false;

        if (string.IsNullOrEmpty(externalMetadata.CoverUrl)) return false;

        if (series.CoverImageLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Covers))
        {
            return false;
        }

        if (string.IsNullOrEmpty(externalMetadata.CoverUrl))
        {
            return false;
        }

        await DownloadSeriesCovers(series, externalMetadata.CoverUrl);
        series.Metadata.AddKPlusOverride(MetadataSettingField.Covers);
        await _auditService.LogAsync(KavitaPlusAuditCategory.Metadata, KavitaPlusEventType.CoverUpdated, AuditStatus.Success,
            AuditSubjectType.Series, seriesId: series.Id,
            payload: new AuditLogSeriesCoverParamsDto { SeriesName = series.Name, CoverUrl = externalMetadata.CoverUrl });
        return true;
    }


    private static (bool, MetadataFieldChangeDto?) UpdateReleaseYear(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableStartDate) return (false, null);

        if (!externalMetadata.StartDate.HasValue) return (false, null);

        if (series.Metadata.ReleaseYearLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.StartDate))
        {
            return (false, null);
        }

        if (series.Metadata.ReleaseYear != 0 && !HasForceOverride(settings, series.Metadata, MetadataSettingField.StartDate))
        {
            return (false, null);
        }

        var from = series.Metadata.ReleaseYear;
        series.Metadata.ReleaseYear = externalMetadata.StartDate.Value.Year;
        series.Metadata.AddKPlusOverride(MetadataSettingField.StartDate);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.ReleaseYear, from, series.Metadata.ReleaseYear));
    }

    private static (bool, MetadataFieldChangeDto?) UpdateLocalizedName(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableLocalizedName) return (false, null);

        if (series.LocalizedNameLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.LocalizedName))
        {
            return (false, null);
        }

        if (!string.IsNullOrWhiteSpace(series.LocalizedName) && !HasForceOverride(settings, series.Metadata, MetadataSettingField.LocalizedName))
        {
            return (false, null);
        }

        var from = series.LocalizedName;

        // We need to make the best appropriate guess
        if (externalMetadata.Name == series.Name)
        {
            // Choose closest (usually last) synonym
            var validSynonyms = externalMetadata.Synonyms
                .Where(IsRomanCharacters)
                .Where(s => s.ToNormalized() != series.Name.ToNormalized())
                .ToList();

            if (validSynonyms.Count == 0) return (false, null);

            series.LocalizedName = validSynonyms[^1];
            series.LocalizedNameLocked = true;
        }
        else if (IsRomanCharacters(externalMetadata.Name))
        {
            series.LocalizedName = externalMetadata.Name;
            series.LocalizedNameLocked = true;
        }


        series.Metadata.AddKPlusOverride(MetadataSettingField.LocalizedName);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.LocalizedName, from, series.LocalizedName));
    }

    private static (bool, MetadataFieldChangeDto?) UpdateSummary(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata)
    {
        if (!settings.EnableSummary) return (false, null);

        if (string.IsNullOrEmpty(externalMetadata.Summary)) return (false, null);

        if (series.Metadata.SummaryLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Summary))
        {
            return (false, null);
        }

        if (!string.IsNullOrWhiteSpace(series.Metadata.Summary) && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Summary))
        {
            return (false, null);
        }

        var from = series.Metadata.Summary;
        series.Metadata.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(externalMetadata.Summary));
        series.Metadata.AddKPlusOverride(MetadataSettingField.Summary);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Summary, from, series.Metadata.Summary));
    }


    private static void Accumulate(ref bool madeModification, List<MetadataFieldChangeDto> changes, (bool Modified, MetadataFieldChangeDto? Change) result)
    {
        madeModification = result.Modified || madeModification;
        if (result.Change != null) changes.Add(result.Change);
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
            // Only choose the better image if we're overriding a user provided cover
            await _coverDbService.SetSeriesCoverByUrl(series, coverUrl, false, !series.Metadata.HasSetKPlusMetadata(MetadataSettingField.Covers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception downloading cover image for Series {SeriesName} ({SeriesId})", series.Name, series.Id);
        }
    }

    private async Task DownloadChapterCovers(Chapter chapter, string coverUrl)
    {
        try
        {
            await _coverDbService.SetChapterCoverByUrl(chapter, coverUrl, false, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception downloading cover image for Chapter {ChapterName} ({SeriesId})", chapter.Range, chapter.Id);
        }
    }

    private async Task DownloadAndSetPersonCovers(List<SeriesStaffDto> people)
    {
        foreach (var staff in people)
        {
            var aniListId = ExternalIdParser.GetAniListStaffId(staff.Url);
            if (aniListId <= 0) continue;
            var person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId);
            if (person == null || string.IsNullOrEmpty(staff.ImageUrl) ||
                !string.IsNullOrEmpty(person.CoverImage) || staff.ImageUrl.EndsWith("default.jpg")) continue;

            try
            {
                await _coverDbService.SetPersonCoverByUrl(person, staff.ImageUrl, false, true);
                await _auditService.LogPersonAsync(KavitaPlusEventType.PersonCoverUpdated, person.Id,
                    new AuditLogPersonCoverParamsDto { PersonName = person.Name, AniListId = aniListId, ImageUrl = staff.ImageUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception saving cover image for Person {PersonName} ({PersonId})", person.Name, person.Id);
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

            if (series.Format is MangaFormat.Epub or MangaFormat.Pdf && chapters.Count == 1)
            {
                series.Metadata.MaxCount = 1;
            }
            else if (series.Metadata.TotalCount <= 1 && chapters is [{ IsSpecial: true }])
            {
                series.Metadata.MaxCount = series.Metadata.TotalCount;
            }
            else if ((maxChapter == Parser.DefaultChapterNumber || maxChapter > series.Metadata.TotalCount) &&
                     maxVolume <= series.Metadata.TotalCount && maxVolume != Parser.DefaultChapterNumber)
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

                if (IsSeriesCompleted(series, chapters, externalMetadata, maxVolume))
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

    /// <summary>
    /// Returns true if the series should be marked as completed, checks loosey with chapter and series numbers.
    /// Respects Specials to reach the required amount.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="chapters"></param>
    /// <param name="externalMetadata"></param>
    /// <param name="maxVolumes"></param>
    /// <returns></returns>
    /// <remarks>Updates MaxCount and TotalCount if a loosey check is used to set as completed</remarks>
    public static bool IsSeriesCompleted(Series series, List<Chapter> chapters, ExternalSeriesDetailDto externalMetadata, int maxVolumes)
    {
        // A series is completed if exactly the amount is found
        if (series.Metadata.MaxCount == series.Metadata.TotalCount && series.Metadata.TotalCount > 0)
        {
            return true;
        }

        // If volumes are collected, check if we reach the required volumes by including specials, and decimal volumes
        //
        // TODO BUG: If the series has specials, that are not included in the  external count. But you do own them
        //           This may mark the series as completed pre-maturely
        // Note: I've currently opted to keep this an equals to prevent the above bug from happening
        // We *could* change this to >= in the future in case this is reported by users
        // If we do; test IsSeriesCompleted_Volumes_TooManySpecials needs to be updated
        if (maxVolumes != Parser.DefaultChapterNumber && externalMetadata.Volumes == series.Volumes.Count)
        {
            series.Metadata.MaxCount = series.Volumes.Count;
            series.Metadata.TotalCount = series.Volumes.Count;
            return true;
        }

        // Note: If Kavita has specials, we should be lenient and ignore for the volume check
        var volumeModifier = series.Volumes.Any(v => v.Name == Parser.SpecialVolume) ? 1 : 0;
        var modifiedMinVolumeCount = series.Volumes.Count - volumeModifier;
        if (maxVolumes != Parser.DefaultChapterNumber && externalMetadata.Volumes == modifiedMinVolumeCount)
        {
            series.Metadata.MaxCount = modifiedMinVolumeCount;
            series.Metadata.TotalCount = modifiedMinVolumeCount;
            return true;
        }

        // If no volumes are collected, the series is completed if we reach or exceed the external chapters
        if (maxVolumes == Parser.DefaultChapterNumber && series.Metadata.MaxCount >= externalMetadata.Chapters)
        {
            series.Metadata.TotalCount = series.Metadata.MaxCount;
            return true;
        }

        // If no volumes are collected, the series is complete if we reach or exceed the external chapters while including
        // prologues, and extra chapters
        if (maxVolumes == Parser.DefaultChapterNumber && chapters.Count >= externalMetadata.Chapters)
        {
            series.Metadata.TotalCount = chapters.Count;
            series.Metadata.MaxCount = chapters.Count;
            return true;
        }


        return false;
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
            var matchingMappings = mappings.Where(m =>
                m.SourceType == sourceType &&
                m.SourceValue.ToNormalized().Equals(value.ToNormalized()));

            var keepOriginal = true;

            foreach (var mapping in matchingMappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.DestinationValue)))
            {
                result[mapping.DestinationType].Add(mapping.DestinationValue);

                // Only keep the original tags if none of the matches want to remove it
                keepOriginal = keepOriginal && !mapping.ExcludeFromSource;
            }

            if (keepOriginal)
            {
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
        mappings = mappings
            .GroupBy(m => m.Key.ToNormalized())
            .ToDictionary(
                g => g.Key,
                g => g.Max(m => m.Value)
            );

        return values
            .Select(v => mappings.GetValueOrDefault(v.ToNormalized(), AgeRating.Unknown))
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
            var seriesForRec = await _unitOfWork.SeriesRepository.GetSeriesDtoByNamesAndMetadataIdsAsync(rec.RecommendationNames,
                libraryType, ScrobblingHelper.CreateUrl(ScrobblingService.AniListWeblinkWebsite, rec.AniListId),
                ScrobblingHelper.CreateUrl(ScrobblingService.MalWeblinkWebsite, rec.MalId));

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


    /// <summary>
    /// This is to get series information for the recommendation drawer on Kavita
    /// </summary>
    /// <remarks>This uses a different API that series detail</remarks>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task<ExternalSeriesDetailDto?> GetSeriesDetail(int? aniListId, long? malId, int? seriesId, CancellationToken ct = default)
    {
        // TODO: This is the primary point where we need to integrate ExternalIds since weblink parsing is already handled
        // TODO: Ensure when we set/update weblinks via API, we reparse and update external ids (if they are empty only)
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
                SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.ExternalReviews, ct);
            if (series != null)
            {
                if (payload.AniListId <= 0)
                {
                    payload.AniListId = ExternalIdParser.GetAniListId(series.Metadata.WebLinks);
                }
                if (payload.MalId <= 0)
                {
                    payload.MalId = ExternalIdParser.GetMalId(series.Metadata.WebLinks);
                }
                payload.SeriesName = series.Name;
                payload.LocalizedSeriesName = series.LocalizedName;
                payload.PlusMediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format);
            }

        }
        try
        {
            var ret =  await _kavitaPlusApiService.GetSeriesDetailByIdAsync(payload, ct);

            ret.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(ret.Summary));

            return ret;

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    private static bool HasForceOverride(MetadataSettingsDto settings, IHasKPlusMetadata kPlusMetadata,
        MetadataSettingField field)
    {
        return settings.HasOverride(field) || kPlusMetadata.HasSetKPlusMetadata(field);
    }
}
