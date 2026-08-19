using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
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
using Kavita.Models.Entities.Person;
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
    private readonly string[] _artistRoleStrings = [
        "Art", "Story & Art",  // AniList
        "Artist", // MangaBaka, Hardcover
        "Illustrations", "Cover Artist", "Illustrator" // Hardcover
    ];
    private readonly string[] _writerRoleStrings = [
        "Story", "Story & Art", // AniList
        "Author", // MangaBaka, Hardcover
    ];
    private readonly SeriesDetailPlusDto _defaultReturn = new()
    {
        Series =  null,
        Recommendations = null,
        Ratings = [],
        Reviews = []
    };

    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> SeriesWriteLocks = new();
    private static SemaphoreSlim GetSeriesWriteLock(int seriesId) => SeriesWriteLocks.GetOrAdd(seriesId, static _ => new SemaphoreSlim(1, 1));
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
        return KavitaPlusConfiguration.MetadataProvidersForLibraryTypes.ContainsKey(type);
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
            _logger.LogDebug("[Kavita+ Data Refresh] No series need matching or refreshing (stale data)");
            return;
        }


        _logger.LogDebug("[Kavita+ Data Refresh] Started Refreshing {Count} series data from Kavita+: {Ids}", ids.Count, string.Join(',', ids));
        var count = 0;
        var successfulMatches = new List<int>();
        var libTypes = await _unitOfWork.LibraryRepository.GetLibraryTypesBySeriesIdsAsync(ids, ct);
        foreach (var seriesId in ids)
        {
            var libraryType = libTypes[seriesId];
            var success = await TryMatchAndLoadMetadataForSeries(seriesId, libraryType, MetadataFetchTrigger.ScheduledRefresh, ct) != null;
            if (success)
            {
                count++;
                successfulMatches.Add(seriesId);
            }
            await Task.Delay(10000, ct); // Currently AL is degraded and has 30 requests/min, give a little padding since this is a background request
        }
        _logger.LogDebug("[Kavita+ Data Refresh] Finished Refreshing {Count} / {Total} series data from Kavita+: {Ids}", count, ids.Count, string.Join(',', successfulMatches));
    }

    public async Task<SeriesDetailPlusDto?> TryMatchAndLoadMetadataForSeries(int seriesId, LibraryType libraryType, MetadataFetchTrigger trigger,
        CancellationToken ct = default)
    {
        if (!IsPlusEligible(libraryType)) return null;
        if (!await _licenseService.HasActiveLicense(ct: ct)) return null;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library | SeriesIncludes.Chapters, ct: ct);
        if (series == null) return null;

        if (!series.WillScrobble() || !series.Library.AllowMetadataMatching) return null;

        // OnDemand (Page visit) is allowed to bypass the rate limit to allow for a nicer user experience
        // TODO: Check if this is correct. Do we want a stricter RateLimit on it?
        if (trigger != MetadataFetchTrigger.OnDemand && !RateLimiter.TryAcquire(string.Empty))
        {
            _logger.LogDebug("Skipping Matching for Series {SeriesId} due to rate limit", seriesId);
            return null;
        }

        if (HasRequiredId(series, series.GetEffectiveMetadataProvider()))
        {
            return await GetSeriesDetailPlus(seriesId, libraryType, trigger, ct: ct);
        }

        var matchRequest = new MatchRequestV3Dto
        {
            AniListId = series.AniListId,
            MalId = series.MalId,
            HardcoverId = series.HardcoverId,
            CbrId = series.CbrId,
            MangabakaId = series.MangaBakaId,
            MetronId = series.MetronId,
            ComicVineId = series.ComicVineId,
            IsStandAlone = series.Volumes.Sum(v => v.Chapters.Count) == 1,
            Provider = series.GetEffectiveMetadataProvider(),
            SeriesName = series.Name,
            AlternativeNames = ExtractAlternativeNames(series),
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
        };

        var result = await _kavitaPlusApiService.MatchSeriesV3Async(matchRequest, ct);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to load matches for series {SeriesId} from Kavita+: {Error}", seriesId, result.ErrorMessage);
            return null;
        }

        var match = await PickBestMatch(series, result.Data, ct);
        if (match == null) return null;

        _logger.LogDebug("Matches series {SeriesId} to MangaBaka: {MangaBakaId}, HardcoverId: {HardcoverId}, CbrId: {CbrId} with {Certainty}% certainty",
            seriesId, match.Series.MangabakaId, match.Series.HardcoverId, match.Series.CbrId, match.MatchRating * 100);

        var beforeIds = new AuditLogMatchExternalIdsParamsDto
        {
            AniListId = series.AniListId,
            MalId = series.MalId,
            MangaBakaId = series.MangaBakaId,
            MangaBakaEditionId = series.MangaBakaEditionId,
            CbrId = series.CbrId,
            HardcoverId = series.HardcoverId,
        };

        series.MangaBakaId = match.Series.MangabakaId ?? 0;
        series.AniListId = match.Series.AniListId ?? 0;
        series.MalId = match.Series.MALId ?? 0;
        series.HardcoverId = match.Series.HardcoverId ?? 0;
        series.CbrId = match.Series.CbrId ?? 0;
        series.IsStandAlone = match.Series.IsStandAlone;

        if (series.GetEffectiveMetadataProvider() == MetadataProvider.Mangabaka)
        {
            var editionMatch = PickBestEdition(series, match.Series.Editions);

            if (editionMatch != null)
            {
                _logger.LogInformation("Matches series {SeriesId} to MangaBaka Edition: {EditionId}", series.Id, editionMatch.Id);
                series.MangaBakaEditionId = editionMatch.Id;
            }
        }

        await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMatched, seriesId,
            new AuditLogMatchedParamsDto {
                SeriesName = series.Name,
                Before = beforeIds, After = new AuditLogMatchExternalIdsParamsDto
                {
                    AniListId = series.AniListId,
                    MalId = series.MalId,
                    MangaBakaId = series.MangaBakaId,
                    MangaBakaEditionId = series.MangaBakaEditionId,
                    CbrId = series.CbrId,
                    HardcoverId = series.HardcoverId,
                },
                MatchedName = series.Name
            }, ct: ct);

        await _unitOfWork.CommitAsync(ct);

        // Force a refresh: the match just set new external Ids, so any previously cached metadata no longer applies.
        return await GetSeriesDetailPlus(seriesId, libraryType, trigger, forceRefresh: true, ct: ct);
    }

    private static bool HasRequiredId(Series series, MetadataProvider metadataProvider)
    {
        return metadataProvider switch
        {
            MetadataProvider.Hardcover => series.HardcoverId > 0,
            MetadataProvider.Mangabaka => series.MangaBakaId > 0 || series.AniListId > 0 || series.MalId > 0,
            MetadataProvider.ComicBookRoundup => series.CbrId > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(metadataProvider), metadataProvider, null)
        };
    }

    private async Task<ExternalSeriesMatchDto?> PickBestMatch(Series series, IList<ExternalSeriesMatchDto> matches, CancellationToken ct)
    {
        var perfectAutomatedMatches = matches
            .Where(m => m.MatchRating == 1f)
            .ToList();

        // Exactly one perfect match, use it regardless of other good (> 0.9) matches
        if (perfectAutomatedMatches.Count == 1)
        {
            return perfectAutomatedMatches[0];
        }

        var validAutomatedMatches = matches
            .Where(m => m.MatchRating > 0.9)
            .OrderBy(m => m.MatchRating)
            .ToList();

        // Exactly one good enough match, use it
        if (validAutomatedMatches.Count == 1)
        {
            return validAutomatedMatches[0];
        }

        if (validAutomatedMatches.Count == 0)
        {
            series.IsBlacklisted = true;
            await _unitOfWork.CommitAsync(ct);

            await _auditService.LogAsync(KavitaPlusAuditCategory.Match, KavitaPlusEventType.SeriesBlacklisted,
                AuditStatus.Failure, seriesId: series.Id, error: "no-matches", ct: ct);

            _logger.LogDebug("No good enough matches out of {TotalMatch} found for Series {SeriesId}",matches.Count, series.Id);
            return null;
        }

        series.IsBlacklisted = true;
        await _unitOfWork.CommitAsync(ct);

        await _auditService.LogAsync(KavitaPlusAuditCategory.Match, KavitaPlusEventType.SeriesBlacklisted,
            AuditStatus.Failure, seriesId: series.Id, error: "too-many-matches", ct: ct);

        _logger.LogDebug("Found {GoodMatch} good enough matches out of {TotalMatch} found for Series {SeriesId}. Will not automatically choose",
            validAutomatedMatches.Count, matches.Count, series.Id);
        return null;
    }

    private static ExternalEditionDto? PickBestEdition(Series series, IList<ExternalEditionDto> editions)
    {
        // No other options, use the present one so we get at least some volume/chapter metadata
        if (editions.Count == 1)
        {
            return editions[0];
        }

        var parsedSeriesEdition = string.Empty; // TODO (Joe): Parse edition from series XXX
        var parsedSeriesEditionMatches = editions
            .Where(e => e.Format.Equals(parsedSeriesEdition, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var digitalEditions = editions
            .Where(e => e.Format.Equals("Digital", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // As these are automatic matches we want exactly one match for certainty
        return parsedSeriesEditionMatches.OneOrDefault()
            ?? parsedSeriesEditionMatches.OneOrDefault(MatchEditionToCount)
            ?? digitalEditions.OneOrDefault()
            ?? digitalEditions.OneOrDefault(MatchEditionToCount)
            ?? editions.OneOrDefault(MatchEditionToCount);

        bool MatchEditionToCount(ExternalEditionDto edition)
        {
            var seriesCount = edition.Type switch
            {
                EditionEntryType.Volume => series.Volumes.Count,
                EditionEntryType.Chapter or EditionEntryType.Other => series.Volumes.Sum(v => v.Chapters.Count),
                _ => throw new ArgumentOutOfRangeException(nameof(edition.Type), edition.Type, null)
            };

            if (edition.Type != EditionEntryType.Other)
            {
                return seriesCount == edition.MainCount;
            }

            return seriesCount == edition.MainCount || seriesCount == edition.TotalCount;
        }
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

    /// <summary>
    /// Searches against Kavita+ for potential matched series/standalone books.
    /// </summary>
    /// <remarks>Explicitly does not include external ids if query is non-empty</remarks>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<MatchSeriesResultDto?> MatchSeries(MatchSeriesDto dto, CancellationToken ct = default)
    {
        const SeriesIncludes includes = SeriesIncludes.Metadata | SeriesIncludes.ExternalMetadata | SeriesIncludes.Library;
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId, includes, ct);
        if (series == null) return null;

        var queried = ParseQueriedIds(dto.Query);

        var provider = queried.Provider ?? dto.Provider ?? series.GetEffectiveMetadataProvider();

        var matchV3Request = BuildMatchRequest(series, dto, queried, provider);

        _logger.LogDebug("Making match request for series {SeriesId}: {@Request}", series.Id, matchV3Request);

        var kPlusResult = await _kavitaPlusApiService.MatchSeriesV3Async(matchV3Request, ct);
        if (!kPlusResult.IsSuccess)
        {
            _logger.LogError("Match request failed for {SeriesName}: {Error}", series.Name, kPlusResult.ErrorMessage);
            return new MatchSeriesResultDto {Provider = provider};
        }

        var results = kPlusResult.Data;

        // Some summaries can contain multiple <br/>s, we need to ensure it's only 1
        foreach (var result in results)
        {
            result.Series.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(result.Series.Summary));
        }

        return new MatchSeriesResultDto
        {
            Provider = provider,
            Matches = results,
        };
    }

    private sealed record QueriedExternalIds
    {
        public int? AniListId { get; init; }
        public long? MalId { get; init; }
        public long MangaBakaId { get; init; }
        public string? HardcoverSlug { get; init; }
        public string? CbrSlug { get; init; }
        /// <summary>
        /// Set when the queried url itself says if it points at a single book or at a series
        /// </summary>
        public bool? IsStandAlone { get; init; }

        /// <summary>
        /// If any id was found, the raw query string is meaningless to Kavita+ and shouldn't be sent along
        /// </summary>
        public bool HasAny => AniListId.HasValue || MalId.HasValue || MangaBakaId > 0
                              || !string.IsNullOrEmpty(HardcoverSlug) || !string.IsNullOrEmpty(CbrSlug);

        /// <summary>
        /// The provider that owns these ids, if the user queried a provider-specific url/header
        /// </summary>
        public MetadataProvider? Provider
        {
            get
            {
                if (!string.IsNullOrEmpty(HardcoverSlug)) return MetadataProvider.Hardcover;
                if (!string.IsNullOrEmpty(CbrSlug)) return MetadataProvider.ComicBookRoundup;
                if (MangaBakaId > 0 || AniListId.HasValue || MalId.HasValue) return MetadataProvider.Mangabaka;

                return null;
            }
        }
    }

    private static QueriedExternalIds ParseQueriedIds(string? rawQuery)
    {
        var query = rawQuery ?? string.Empty;
        var hardcoverUrl = ExternalIdParser.GetHardcoverSlugFromUrl(query);

        return new QueriedExternalIds
        {
            AniListId = ExternalIdParser.TryParseAniListHeader(query, out var aniListId)
                ? aniListId : ExternalIdParser.GetAniListId(query),
            MalId = ExternalIdParser.TryParseMalHeader(query, out var malId)
                ? malId : ExternalIdParser.GetMalId(query),
            MangaBakaId = ExternalIdParser.TryParseMangaBakaHeader(query, out var mangaBakaId)
                ? mangaBakaId : ExternalIdParser.GetMangaBakaId(query),
            HardcoverSlug = ExternalIdParser.TryParseHardcoverHeader(query, out var hardcoverSlug)
                ? hardcoverSlug : hardcoverUrl?.Slug,
            // For now, we pass the slug as query as there is a direct handling on query currently
            CbrSlug = query.Contains("comicbookroundup.com/") ? query : null,
            IsStandAlone = hardcoverUrl?.IsStandAlone,
        };
    }

    private static MatchRequestV3Dto BuildMatchRequest(Series series, MatchSeriesDto dto, QueriedExternalIds queried,
        MetadataProvider provider)
    {
        // If any id was extracted, the raw query string is meaningless to the backend
        var query = queried.HasAny ? null : dto.Query;
        var isStandAlone = queried.IsStandAlone ?? dto.IsStandAlone;
        var format = series.Library.Type.ConvertToPlusMediaFormat(series.Format);
        var webLinks = series.Metadata.WebLinks;

        // Only use series ids if no ids have been supplied via the query
        var aniListId = queried.HasAny ? queried.AniListId : series.AniListId;
        var malId = queried.HasAny ? queried.MalId : series.MalId;
        var mangaBakaId = queried.HasAny ? queried.MangaBakaId : series.MangaBakaId;

        return new MatchRequestV3Dto
        {
            AniListId = aniListId,
            MalId = malId,
            HardcoverId = isStandAlone ? ExternalIdParser.GetHardcoverBookId(webLinks) : ExternalIdParser.GetHardcoverSeriesId(webLinks),
            Slug = provider switch
            {
                MetadataProvider.Hardcover => queried.HardcoverSlug,
                MetadataProvider.ComicBookRoundup => queried.CbrSlug,
                _ => string.Empty,
            },
            CbrId = null,
            MangabakaId = mangaBakaId,
            IsStandAlone = isStandAlone,
            Provider = provider,
            SeriesName = series.Name,
            AlternativeNames = ExtractAlternativeNames(series),
            Year = GetReleaseYear(series, format),
            Query = query,
            Format = format,
        };
    }

    /// <summary>
    /// Comics rarely carry a release year on their metadata, but often have one in their name
    /// </summary>
    private static int GetReleaseYear(Series series, PlusMediaFormat format)
    {
        var year = series.Metadata.ReleaseYear;
        if (year != 0 || format != PlusMediaFormat.Comic || string.IsNullOrWhiteSpace(series.Name)) return year;

        var potentialYear = Parser.ParseYear(series.Name);

        return string.IsNullOrEmpty(potentialYear) ? year : int.Parse(potentialYear);
    }

    private static List<string> ExtractAlternativeNames(Series series)
    {
        List<string> altNames = [series.LocalizedName, series.OriginalName];
        return altNames.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
    }


    /// <summary>
    /// Fetches metadata about an external Series
    /// </summary>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? mangaBakaId, int? seriesId, CancellationToken ct = default)
    {
        if (!aniListId.HasValue && !malId.HasValue && !mangaBakaId.HasValue && !seriesId.HasValue)
        {
            throw new KavitaException("Unable to find valid information from url for External Load");
        }

        // This is for the Series drawer. We can get this extra information during the initial SeriesDetail call so it's all coming from the DB
        return await GetSeriesDetail(aniListId, malId, mangaBakaId, seriesId, ct);
    }

    private async Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!IsPlusEligible(libraryType) || !await _licenseService.HasActiveLicense(ct: ct)) return _defaultReturn;

        // Check blacklist (bad matches) or if there is a don't match
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library,  ct: ct);
        if (series == null || !series.WillScrobble() || !series.Library.AllowMetadataMatching) return _defaultReturn;

        // After a fresh match the external Ids just changed, so any cached data is stale by definition and must be refetched
        var needsRefresh = forceRefresh ||
            await _unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(seriesId, ct);

        if (!needsRefresh)
        {
            // Convert into DTOs and return
            return await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(seriesId, ct);
        }

        var data = await _unitOfWork.SeriesRepository.GetKavitaPlusSeriesDetailRequestV3Dto(seriesId, ct);
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

    public async Task FixSeriesMatch(int seriesId, ExternalMetadataIdsDto ids, MetadataProvider? provider = null, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library, ct);
        if (series == null) return;

        // The user matched against a provider that isnt the series own, so that choice becomes the series provider
        if (provider.HasValue && provider.Value != series.GetEffectiveMetadataProvider())
        {
            await UpdateSeriesMetadataProviderOverride(seriesId, provider.Value, ct);
        }

        // Remove from Blacklist
        series.IsBlacklisted = false;
        series.DontMatch = false;
        _unitOfWork.SeriesRepository.Update(series);
        _fileCacheService.InvalidatePrefix(GetCoversCacheKey(seriesId), FileCacheService.KavitaPlusCacheDirectory);

        // Refetch metadata with a Direct lookup
        try
        {
            var metadata = await FetchExternalMetadataForSeries(seriesId, series.Library.Type,
                new  SeriesDetailRequestV3Dto()
                {
                    Provider = series.GetEffectiveMetadataProvider(),
                    AniListId = ids.AniListId,
                    MalId = ids.MalId,
                    CbrId = ids.CbrId,
                    MangabakaId = ids.MangabakaId,
                    MangaBakaEditionId = ids.MangaBakaEditionId,
                    HardcoverId = ids.HardcoverId,
                    IsStandAlone = ids.IsStandAlone,
                    Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
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

    public async Task UpdateSeriesMetadataProviderOverride(int seriesId, MetadataProvider? metadataProviderOverride, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
            SeriesIncludes.Library | SeriesIncludes.ExternalMetadata, ct);
        if (series == null) return;

        if (series.Library.MetadataProvider == metadataProviderOverride)
        {
            metadataProviderOverride = null;
        }

        if (series.MetadataProviderOverride == metadataProviderOverride) return;

        var previousProvider = series.GetEffectiveMetadataProvider();
        series.MetadataProviderOverride = metadataProviderOverride;
        var newProvider = series.GetEffectiveMetadataProvider();

        // Pinning the Library's current default (or dropping back to it) doesn't change who we match against,
        // so the data we already hold is still from the right provider
        if (previousProvider != newProvider)
        {
            _logger.LogInformation("Series {SeriesName} is switching Metadata Provider from {PreviousProvider} to {NewProvider}",
                series.Name, previousProvider, newProvider);

            // Everything cached came from the previous provider and must be refetched rather than served from cache
            var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalReviews);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRatings);
            _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRecommendations);
            externalSeriesMetadata.ValidUntilUtc = DateTime.MinValue;
            _fileCacheService.InvalidatePrefix(GetCoversCacheKey(seriesId), FileCacheService.KavitaPlusCacheDirectory);

            // Failing to match against the previous provider says nothing about the new one
            series.IsBlacklisted = false;
        }

        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync(ct);

        await _eventHub.SendMessageAsync(MessageFactory.SeriesUpdated, MessageFactory.SeriesUpdatedEvent(series.Id), ct: ct);

        await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMetadataProviderOverrideSet, seriesId,
            new AuditLogMatchProviderOverrideParamsDto
            {
                SeriesName = series.Name,
                PreviousProvider = previousProvider,
                NewProvider = newProvider,
                IsOverride = metadataProviderOverride.HasValue,
            }, ct: ct);
    }

    /// <summary>
    /// Requests the full SeriesDetail (rec, review, metadata) data for a Series. Will save to ExternalMetadata tables.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="data"></param>
    /// <param name="trigger"></param>
    /// <param name="ct"></param>
    /// <param name="fromMatchFlow"></param>
    /// <returns></returns>
    private async Task<SeriesDetailPlusDto> FetchExternalMetadataForSeries(int seriesId, LibraryType libraryType, SeriesDetailRequestV3Dto data,
        bool fromMatchFlow = false, MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default)
    {

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library | SeriesIncludes.Metadata, ct);
        if (series?.Library == null)
        {
            return _defaultReturn;
        }

        _logger.LogDebug("Fetching Kavita+ Series Detail data for {SeriesName}", string.IsNullOrEmpty(data.SeriesName) ? data.AniListId : data.SeriesName);

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

        var kPlusResult = await _kavitaPlusApiService.GetSeriesDetailV3Async(data, ct);
        if (!kPlusResult.IsSuccess && (kPlusResult.ErrorMessage ?? string.Empty).Contains("Too many Requests"))
        {
            _logger.LogDebug("Hit the rate limit while fetching Kavita+ Series Detail data for {SeriesId}. Retrying in 3s", series.Id);
            await Task.Delay(3000, ct);

            kPlusResult = await _kavitaPlusApiService.GetSeriesDetailV3Async(data, ct);
        }

        if (kPlusResult.ErrorMessage.IsUnknownSeriesError())
        {
            series.IsBlacklisted = true;
            await _unitOfWork.CommitAsync(ct);
            await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesBlacklisted, seriesId,
                new AuditLogMatchFailureParamsDto { SeriesName = series.Name, Reason = "unknown-series" }, AuditStatus.Failure, ct: ct);
            return _defaultReturn;
        }

        var result = kPlusResult.Data;

        if (result == null)
        {
            _logger.LogError("Unable to fetch Kavita+ Series Detail data for {SeriesId}: {ErrorMessage}",
                series.Id, kPlusResult.ErrorMessage);

            var reason = (kPlusResult.ErrorMessage ?? string.Empty).Contains("Too Many Requests")
                ? "rate-limit-hit" : kPlusResult.ErrorMessage ?? string.Empty;

            await _auditService.LogMatchAsync(KavitaPlusEventType.SeriesMatchFailed, seriesId,
                new AuditLogMatchFailureParamsDto { SeriesName = series.Name, Reason = reason }, AuditStatus.Failure, ct: ct);
            return _defaultReturn;
        }


        // Clear out existing results
        var externalSeriesMetadata = await GetOrCreateExternalSeriesMetadataForSeries(seriesId, series);
        _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalReviews);
        _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRatings);
        _unitOfWork.ExternalSeriesMetadataRepository.Remove(externalSeriesMetadata.ExternalRecommendations);

        series.IsStandAlone = result.Series?.IsStandAlone ?? false;
        externalSeriesMetadata.Provider = data.Provider;

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

        // User-base runs first so that a duplicate prefers User-base
        externalSeriesMetadata.ExternalRecommendations ??= [];
        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto(ct);
        var seenRecommendations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recs = await ProcessRecommendations(libraryType, result.ReadersAlsoLike, externalSeriesMetadata,
            RecommendationSource.UserBased, series.GetEffectiveMetadataProvider(), metadataSettings, seenRecommendations);
        var similarRecs = await ProcessRecommendations(libraryType, result.SimilarSeries, externalSeriesMetadata,
            RecommendationSource.Similar, series.GetEffectiveMetadataProvider(), metadataSettings, seenRecommendations);
        recs.ExternalSeries = recs.ExternalSeries.Concat(similarRecs.ExternalSeries).ToList();
        recs.OwnedSeries = recs.OwnedSeries.Concat(similarRecs.OwnedSeries).ToList();

        var extRatings = externalSeriesMetadata.ExternalRatings
            .Where(r => r.AverageScore > 0)
            .ToList();

        externalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.Add(_externalSeriesMetadataCache);
        externalSeriesMetadata.AverageExternalRating = extRatings.Count != 0 ? (int) extRatings
            .Average(r => r.AverageScore) : 0;

        // prefer what was passed in (manual match), fall back to what K+ returned
        var beforeIds = new AuditLogMatchExternalIdsParamsDto { AniListId = series.AniListId, MalId = series.MalId,
            MangaBakaId = series.MangaBakaId, MangaBakaEditionId = series.MangaBakaEditionId, CbrId = series.CbrId, HardcoverId = series.HardcoverId };

        if (!string.IsNullOrEmpty(data.MangaBakaEditionId))
        {
            series.MangaBakaEditionId = data.MangaBakaEditionId;
        }
        else if (series.MangaBakaId == 0)
        {
            series.MangaBakaEditionId = string.Empty;
        }

        // Update ids from K+ in case of merges upstream
        series.AniListId = result.AniListId ?? series.AniListId;
        series.MalId = result.MalId ?? series.MalId;
        series.MangaBakaId = result.MangabakaId ?? series.MangaBakaId;
        series.CbrId = result.CbrId ?? series.CbrId;
        series.HardcoverId = result.HardCoverId ?? series.HardcoverId;

        var afterIds = new AuditLogMatchExternalIdsParamsDto {
            AniListId = series.AniListId,
            MalId = series.MalId,
            MangaBakaId = series.MangaBakaId,
            MangaBakaEditionId = series.MangaBakaEditionId,
            CbrId = series.CbrId,
            HardcoverId = series.HardcoverId };

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

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
        }

        if (madeMetadataModification)
        {
            // Inform the UI of the update
            await _eventHub.SendMessageAsync(MessageFactory.ExternalMetadataUpdate, MessageFactory.ExternalMetadataUpdateEvent(series.Id), false, ct);
        }

        // Volume and MangaBaka chapter covers are not returned inline in the Series detail response, so fetch and apply them separately
        try
        {
            await ApplyExternalCovers(series, metadataSettings, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Covers] Failed to apply external covers for Series {SeriesId}", series.Id);
        }

        return new SeriesDetailPlusDto
        {
            Recommendations = recs,
            Ratings = result.Ratings,
            Reviews = externalSeriesMetadata.ExternalReviews.Select(r => _mapper.Map<UserReviewDto>(r)),
            Series = result.Series
        };
    }

    public async Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId, MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default)
    {
        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto(ct);
        if (!settings.Enabled) return false;

        var writeLock = GetSeriesWriteLock(seriesId);
        await writeLock.WaitAsync(ct);

        try
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Related, ct);
            if (series == null) return false;

            var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct);

            _logger.LogInformation("Writing External metadata to Series {SeriesName}", series.Name);

            var madeModification = false;
            var fieldChanges = new List<MetadataFieldChangeDto>();
            var processedGenres = new List<string>();
            var processedTags = new List<string>();


            Accumulate(ref madeModification, fieldChanges, UpdateSummary(series, settings, externalMetadata));
            Accumulate(ref madeModification, fieldChanges, UpdateReleaseYear(series, settings, externalMetadata));
            Accumulate(ref madeModification, fieldChanges, await UpdatePublicationStatus(series, settings, externalMetadata));
            Accumulate(ref madeModification, fieldChanges, UpdateExternalIds(series, externalMetadata));


            // Apply field mappings
            GenerateGenreAndTagLists(externalMetadata, settings, ref processedTags, ref processedGenres);

            // Since tag mappings outputs a list of strings, we need to find all the tags that will be removed first, then map,
            // then remove those that survived before writing (not age rating mapping)
            var tagsToRemove = GetTagsToRemove(externalMetadata, settings);

            // Filter out by tag-weight
            processedTags = processedTags.Where(pt => !tagsToRemove.Contains(pt)).ToList();

            Accumulate(ref madeModification, fieldChanges, await UpdateGenres(series, settings, externalMetadata, processedGenres));
            Accumulate(ref madeModification, fieldChanges, await UpdateTags(series, settings, externalMetadata, processedTags));

            // In order to ensure that a filtered weight tag doesn't get excluded, age rating is processed on ALL tags + our remapped ones
            var allTags = externalMetadata.Tags.Select(t => t.Name)
                .Concat(externalMetadata.Genres)
                .Concat(processedGenres).Concat(processedTags)
                .Distinct()
                .ToList();

            Accumulate(ref madeModification, fieldChanges, UpdateAgeRating(series, settings, externalMetadata, allTags));

            var staff = await SetNameAndAddAliases(settings, externalMetadata.Staff);

            // TODO: I can update Publisher as well but MB is not fully vetted out yet
            Accumulate(ref madeModification, fieldChanges, await UpdateWriters(series, settings, staff));
            Accumulate(ref madeModification, fieldChanges, await UpdateArtists(series, settings, staff));
            Accumulate(ref madeModification, fieldChanges, await UpdateCharacters(series, settings, externalMetadata.Characters));

            Accumulate(ref madeModification, fieldChanges, await UpdateRelationships(series, settings, externalMetadata.Relations, defaultAdmin));

            if (settings.EnableName || settings.EnableLocalizedName)
            {
                var (namePriority, localizedNamePriority) = ResolveTitleLanguagePriorities(settings, series.LibraryId);

                // One query serves both writes. The set unions NormalizedName/NormalizedLocalizedName/NormalizedOriginalName
                // for every OTHER series in this library+format - a collision there makes the scanner's SingleOrDefault throw.
                var takenNames = await _unitOfWork.SeriesRepository.GetTakenNormalizedNamesInLibraryAsync(
                    series.LibraryId, series.Format, series.Id, ct);

                // Name must be first, LocalizedName will drop the language code that Name eats
                Accumulate(ref madeModification, fieldChanges,
                    await UpdateName(series, settings, externalMetadata, namePriority, takenNames, ct));

                var heldNameLanguageCode = settings.EnableName
                    ? FindHeldLanguageCode(namePriority, externalMetadata, series.NormalizedName)
                    : null;

                Accumulate(ref madeModification, fieldChanges,
                    await UpdateLocalizedName(series, settings, externalMetadata, localizedNamePriority, heldNameLanguageCode, takenNames, ct));
            }

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
        finally
        {
            writeLock.Release();
        }
    }

    private static HashSet<string> GetTagsToRemove(ExternalSeriesDetailDto externalMetadata, MetadataSettingsDto settings)
    {
        var whitelist = settings.Whitelist is { Count: > 0 }
            ? settings.Whitelist.Select(s => s.ToNormalized()).ToHashSet()
            : null;

        if (settings.FilterAboveWeight == null) return [];

        return externalMetadata.Tags
            .Where(t => t.TagWeight != null && t.TagWeight > settings.FilterAboveWeight && whitelist?.Contains(t.Name.ToNormalized()) != true)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Fetches volume and chapter covers from the Kavita+ covers endpoint and applies the best matches. Series and chapter
    /// covers arrive inline in the Series detail response for comic providers; volume covers (and MangaBaka chapter covers)
    /// are not.
    /// </summary>
    /// <remarks>Not run for ComicBookRoundup, whose covers come from chapter/issue metadata already.</remarks>
    private async Task ApplyExternalCovers(Series series, MetadataSettingsDto settings, CancellationToken ct = default)
    {
        if (!settings.EnableVolumeCoverImage && !settings.EnableChapterCoverImage) return;
        if (series.GetEffectiveMetadataProvider() == MetadataProvider.ComicBookRoundup) return;

        // Prefer the cover based on Series/Library locale
        var locale = series.Metadata.Language ?? series.Library?.DefaultLanguage;

        // All volumes: manga chapters can live in the loose-leaf volume, so we filter loose-leaf/specials only for volume covers
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(series.Id, ct)).ToList();
        if (volumes.Count == 0) return;

        var covers = await GetExternalCovers(series.Id, ct: ct);
        var volumeCovers = covers
            .Where(c => c.Type == ExternalCoverImageType.Volume && c.Number.HasValue && !string.IsNullOrEmpty(c.Url))
            .ToList();
        var chapterCovers = covers
            .Where(c => c.Type is ExternalCoverImageType.Chapter or ExternalCoverImageType.Issue
                        && c.Number.HasValue && !string.IsNullOrEmpty(c.Url))
            .ToList();
        if (volumeCovers.Count == 0 && chapterCovers.Count == 0) return;

        foreach (var volume in volumes)
        {
            var nonSpecialChapters = volume.Chapters.Where(c => !c.IsSpecial).ToList();
            var coveredChapterIds = new HashSet<int>();

            if (settings.EnableChapterCoverImage && chapterCovers.Count > 0)
            {
                foreach (var chapter in nonSpecialChapters)
                {
                    var chapterMatch = chapterCovers.FirstOrDefault(c => c.Number!.Value.Is(chapter.MinNumber));
                    if (chapterMatch == null) continue;

                    try
                    {
                        if (await UpdateChapterCoverImage(chapter, settings, series.Id, chapterMatch.Url))
                        {
                            coveredChapterIds.Add(chapter.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Covers] Failed to set cover for Chapter {ChapterId} in Series {SeriesId}", chapter.Id, series.Id);
                    }
                }
            }

            // Volume covers only apply to real volumes (skip loose-leaf/specials)
            if (!settings.EnableVolumeCoverImage) continue;
            if (volume.MinNumber.Is(Parser.LooseLeafVolumeNumber) || volume.MinNumber.Is(Parser.SpecialVolumeNumber)) continue;
            if (volume.CoverImageLocked && !HasForceOverride(settings, volume, MetadataSettingField.VolumeCovers)) continue;

            try
            {
                if (nonSpecialChapters.Count == 1)
                {
                    // Single-chapter volume reuses its chapter's cover
                    var chapter = nonSpecialChapters[0];

                    // Try and get the locale variant, else fallback to whatever we can


                    // Prefer a chapter-scoped cover, fall back to the volume-scoped one
                    var match = chapterCovers
                                    .Where(c => c.Language == locale)
                                    .FirstOrDefault(c => c.Number!.Value.Is(chapter.MinNumber))
                                ?? volumeCovers
                                    .Where(c => c.Language == locale)
                                    .FirstOrDefault(c => c.Number!.Value.Is(volume.MinNumber));

                    if (match == null)
                    {
                        match = chapterCovers
                                    .FirstOrDefault(c => c.Number!.Value.Is(chapter.MinNumber))
                                ?? volumeCovers
                                    .FirstOrDefault(c => c.Number!.Value.Is(volume.MinNumber));
                    }

                    if (match == null) continue;

                    // If the chapter loop didn't already write it, download onto the chapter now (bypassing the chapter-cover setting)
                    if (!coveredChapterIds.Contains(chapter.Id))
                    {
                        var chooseBetterImage = !chapter.HasSetKPlusMetadata(MetadataSettingField.ChapterCovers);
                        chapter.AddKPlusOverride(MetadataSettingField.ChapterCovers);
                        await _coverDbService.SetChapterCoverByUrl(chapter, match.Url, false, chooseBetterImage, ct);
                    }

                    volume.AddKPlusOverride(MetadataSettingField.VolumeCovers);
                    await _coverDbService.SetVolumeCoverFromChapter(volume, chapter, ct);
                    await LogVolumeCoverAudit(series.Id, volume, match.Url, ct);
                }
                else
                {
                    var match = volumeCovers.FirstOrDefault(c => c.Number!.Value.Is(volume.MinNumber));
                    if (match == null) continue;

                    // Only choose the better image the first time; once K+ owns the cover, overwrite freely
                    var chooseBetterImage = !volume.HasSetKPlusMetadata(MetadataSettingField.VolumeCovers);
                    volume.AddKPlusOverride(MetadataSettingField.VolumeCovers);
                    await _coverDbService.SetVolumeCoverByUrl(volume, match.Url, false, chooseBetterImage, ct);
                    await LogVolumeCoverAudit(series.Id, volume, match.Url, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Covers] Failed to set cover for Volume {VolumeId} ({VolumeName}) in Series {SeriesId}",
                    volume.Id, volume.Name, series.Id);
            }
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
        }
    }

    private async Task LogVolumeCoverAudit(int seriesId, Volume volume, string coverUrl, CancellationToken ct)
    {
        await _auditService.LogAsync(KavitaPlusAuditCategory.Metadata, KavitaPlusEventType.VolumeCoverUpdated, AuditStatus.Success,
            AuditSubjectType.Volume, seriesId: seriesId, subjectId: volume.Id,
            payload: new AuditLogVolumeCoverParamsDto { VolumeNumber = volume.GetNumberTitle(), CoverUrl = coverUrl }, ct: ct);
    }

    public async Task<IList<ExternalCoverResponseDto>> GetExternalCovers(int seriesId, int? volumeId = null, int? chapterId = null, CancellationToken ct = default)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
            SeriesIncludes.Metadata | SeriesIncludes.Chapters | SeriesIncludes.Library, ct: ct);
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
            IsStandAlone = series.IsStandAlone,
            MetadataProvider = series.GetEffectiveMetadataProvider()
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

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateRelationships(Series series, MetadataSettingsDto settings,
        IList<SeriesRelationship>? externalMetadataRelations, AppUser defaultAdmin)
    {
        if (!settings.EnableRelationships) return (false, null);

        if (externalMetadataRelations == null || externalMetadataRelations.Count == 0 || defaultAdmin == null)
        {
            return (false, null);
        }

        var addedRelations = new List<object>();
        foreach (var relation in externalMetadataRelations.Where(r => r.Relation != RelationKind.Parent))
        {
            List<string> names = new [] {
                    relation.SeriesName.PreferredTitle,
                    relation.SeriesName.RomajiTitle,
                    relation.SeriesName.EnglishTitle,
                    relation.SeriesName.NativeTitle}
                .Concat(relation.Series?.Synonyms ?? [])
                .Where(s => !string.IsNullOrEmpty(s)).ToList()!;

            var externalIds = new ExternalMetadataIdsDto
            {
                AniListId = relation.AniListId,
                MalId = relation.MalId,
                MangabakaId = relation.MangabakaId,
                PlusMediaFormat = relation.Format,
            };

            var formatTypes = relation.Format.GetMangaFormats();

            var relatedSeries = await _unitOfWork.SeriesRepository.GetSeriesFromExternalMetadata(
                names,
                formatTypes,
                defaultAdmin.Id,
                externalIds,
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
            addedRelations.Add(new
            {
                relatedSeriesName = relatedSeries.Name,
                relatedSeriesId = relatedSeries.Id,
                relatedSeriesLibraryId = relatedSeries.LibraryId,
                kind = (int) relation.Relation
            });

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
        series.Metadata.CharacterLocked = true;

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Characters, null, externalCharacters.Select(c => c.Name).ToList()));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateArtists(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return (false, null);

        var upstreamArtists = staff
            .Where(s => _artistRoleStrings.Contains(s.Role))
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
                HardcoverId = ExternalIdParser.GetHardcoverStaffId(w.Url),
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
        series.Metadata.CoverArtistLocked = true;

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Artists, null, upstreamArtists.Select(a => a.Name).ToList()));
    }

    private async Task<(bool, MetadataFieldChangeDto?)> UpdateWriters(Series series, MetadataSettingsDto settings, List<SeriesStaffDto> staff)
    {
        if (!settings.EnablePeople) return (false, null);

        var upstreamWriters = staff
            .Where(s => _writerRoleStrings.Contains(s.Role))
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
                HardcoverId = ExternalIdParser.GetHardcoverStaffId(w.Url),
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
        series.Metadata.WriterLocked = true;

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

    private (bool, MetadataFieldChangeDto?) UpdateAgeRating(Series series, MetadataSettingsDto settings, ExternalSeriesDetailDto externalMetadata, IEnumerable<string> allExternalTags)
    {
        if (!settings.EnableAgeRating) return (false, null);

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

            // Find the highest age rating from the different mapping mechanisms
            var externalAgeRating = externalMetadata.AgeRating;
            var baseDerivedAgeRating = !string.IsNullOrEmpty(externalMetadata.AgeRatingRaw) ?
                DetermineAgeRating([externalMetadata.AgeRatingRaw], settings.ExternalAgeRatingMappings)
                : AgeRating.Unknown;
            var tagDerivedAgeRating = DetermineAgeRating(totalTags, settings.AgeRatingMappings);

            var kPlusRating = baseDerivedAgeRating;
            if (string.IsNullOrEmpty(externalMetadata.AgeRatingRaw))
            {
                kPlusRating = externalAgeRating;
            }

            // If the admin set up a raw mapping, then we use that, otherwise fallback to Kavita+'s mapping
            var toSetAgeRating = new[]{from, kPlusRating, tagDerivedAgeRating}.Max();

            if (toSetAgeRating == AgeRating.Unknown || toSetAgeRating == from) return (false, null);

            series.Metadata.AgeRating = toSetAgeRating;
            series.Metadata.AddKPlusOverride(MetadataSettingField.AgeRating);
            series.Metadata.AgeRatingLocked = true;
            return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.AgeRating, from.ToString(), series.Metadata.AgeRating.ToString()));
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
        if (externalMetadata.ChapterDtos == null || externalMetadata.ChapterDtos.Count == 0) return false;

        // Get all volumes and chapters
        var madeModification = false;
        var allChapters =  await _unitOfWork.ChapterRepository.GetAllChaptersForSeries(series.Id);

        List<(Chapter, ExternalChapterDto)> matchedChapters = [];

        if (externalMetadata.IsStandAlone)
        {
            if (series.Volumes.Sum(v => v.Chapters.Count) != 1)
            {
                _logger.LogWarning("Series {SeriesName} ({SeriesId}) has more than one chapter. But is matched against a standalone series Skipping chapter update.", series.Name, series.Id);
                return false;
            }

            if (externalMetadata.ChapterDtos.Count != 1)
            {
                return false;
            }

            matchedChapters.Add((allChapters[0], externalMetadata.ChapterDtos[0]));
        }
        else
        {
            matchedChapters = allChapters
                .Join(
                    externalMetadata.ChapterDtos,
                    chapter => Parser.IsLooseLeafVolume(chapter.Range) ? chapter.Volume.Name : chapter.Range,
                    dto => dto.IssueNumber.Replace(',', '.'), // Ensure comma's are dots
                    (chapter, dto) => (chapter, dto)
                )
                .ToList();
        }

        foreach (var (chapter, potentialMatch) in matchedChapters)
        {
            var usedRange = Parser.IsLooseLeafVolume(chapter.Range) ? chapter.Volume.Name : chapter.Range;
            var usedType = Parser.IsLooseLeafVolume(chapter.Range) ? "Volume" : "Chapter";

            _logger.LogDebug("Updating {SeriesName} ({SeriesId}) - {Type} {ChapterNumber} with metadata. Matched to IssueNumber: {IssueNumber} - HardcoverId: {HardcoverId} - MangaBakaWorkId: {WorkId}",
                series.Name, series.Id, usedType, usedRange, potentialMatch.IssueNumber, potentialMatch.HardcoverId, potentialMatch.MangaBakaWorkId);
            var chapterFieldChanges = new List<MetadataFieldChangeDto>();

            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterTitle(chapter, settings, potentialMatch.Title, series.Name));
            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterSummary(chapter, settings, potentialMatch.Summary));
            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterReleaseDate(chapter, settings, potentialMatch.ReleaseDate));
            Accumulate(ref madeModification, chapterFieldChanges, UpdateChapterAgeRating(chapter, settings, series.Metadata.AgeRating));

            var hasUpdatedPublisher = await UpdateChapterPublisher(chapter, settings, potentialMatch.Publisher);
            if (hasUpdatedPublisher)
            {
                chapter.AddKPlusOverride(MetadataSettingField.ChapterPublisher);
                chapter.PublisherLocked = true;
            }
            madeModification = hasUpdatedPublisher || madeModification;

            madeModification = await UpdateChapterPeople(chapter, settings, PersonRole.CoverArtist, potentialMatch.Artists) || madeModification;
            madeModification = await UpdateChapterPeople(chapter, settings, PersonRole.Writer, potentialMatch.Writers) || madeModification;

            madeModification = await UpdateChapterCoverImage(chapter, settings, series.Id, potentialMatch.CoverImageUrl) || madeModification;
            madeModification = await UpdateExternalChapterMetadata(chapter, settings, potentialMatch) || madeModification;

            if (potentialMatch.HardcoverId is > 0)
            {
                chapterFieldChanges.Add(new MetadataFieldChangeDto(MetadataFieldChangeKind.ExternalIds, new { hardcoverId = chapter.HardcoverId }, new { hardcoverId = potentialMatch.HardcoverId }));
                chapter.HardcoverId = potentialMatch.HardcoverId.Value;
            }

            if (chapterFieldChanges.Count > 0)
            {
                await _auditService.LogChapterMetadataAsync(chapter.Id, series.Id, chapterFieldChanges);
            }

            if (madeModification)
            {
                _unitOfWork.ChapterRepository.Update(chapter);
                await _unitOfWork.CommitAsync();
            }
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

        if (string.IsNullOrWhiteSpace(summary) && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterSummary))
        {
            return (false, null);
        }

        var from = chapter.Summary;
        chapter.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(summary));
        chapter.AddKPlusOverride(MetadataSettingField.ChapterSummary);
        chapter.SummaryLocked = true;

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
        chapter.TitleNameLocked = true;

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Title, from, title));
    }

    private static (bool, MetadataFieldChangeDto?) UpdateChapterAgeRating(Chapter chapter, MetadataSettingsDto settings, AgeRating ageRating)
    {
        if (chapter.AgeRatingLocked && !HasForceOverride(settings, chapter, MetadataSettingField.ChapterAgeRating))
        {
            return (false, null);
        }

        var from = chapter.AgeRating;
        chapter.AgeRating = ageRating;
        chapter.AddKPlusOverride(MetadataSettingField.ChapterAgeRating);
        chapter.AgeRatingLocked = true;

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.AgeRating, from, ageRating));
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
        chapter.ReleaseDateLocked = true;

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

        chapter.AddKPlusOverride(MetadataSettingField.People);
        chapter.LockPersonRole(role);

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
        series.Metadata.ReleaseYearLocked = true;

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.ReleaseYear, from, series.Metadata.ReleaseYear));
    }

    /// <summary>
    /// Resolves which language priority lists apply to a Series. A library override replaces the global settings
    /// outright, including any field left blank on it.
    /// </summary>
    /// <remarks>
    /// A blank field means "no languages", which resolves no candidate and therefore writes nothing. That is
    /// deliberate: overriding a library and clearing a field is how an admin opts that field out for the library.
    /// </remarks>
    private static (IReadOnlyList<string> Name, IReadOnlyList<string> LocalizedName) ResolveTitleLanguagePriorities(
        MetadataSettingsDto settings, int libraryId)
    {
        if (settings.LibraryLanguageTitleOverrides.TryGetValue(libraryId, out var libraryOverride) && libraryOverride != null)
        {
            return (libraryOverride.NamePriority, libraryOverride.LocalizedNamePriority);
        }

        var global = settings.GlobalLanguageTitleSettings;
        return (global.NamePriority, global.LocalizedNamePriority);
    }

    /// <summary>
    /// Yields every candidate title in the admin's priority order, best-first, so a caller can walk to the next
    /// candidate when one is rejected instead of giving up on the whole field.
    /// </summary>
    /// <remarks>
    /// Walks every title within a language, not just the first. K+ orders each list best-first, so when
    /// <c>en[0]</c> collides <c>en[1]</c> is still a better answer than dropping English entirely.
    /// </remarks>
    private static IEnumerable<(string Title, string LanguageCode)> EnumerateTitlesByPriority(
        IReadOnlyList<string> priorities, ExternalSeriesDetailDto externalMetadata)
    {
        // Not gated on LocalizedTitles being non-empty: the {Native} token resolves from Titles instead, and a
        // provider can send a native title with no per-language breakdown at all.
        if (priorities.Count == 0) yield break;

        // K+ sends canonical BCP-47 casing ("ja-Latn", "pt-BR") but admins type freely and System.Text.Json hands
        // us an ordinal dictionary, so "ja-latn" would miss. Re-key case-insensitively, first entry wins.
        var titlesByLanguage = new Dictionary<string, IList<LocalizedTitleDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in externalMetadata.LocalizedTitles)
        {
            titlesByLanguage.TryAdd(pair.Key, pair.Value);
        }

        foreach (var languageCode in priorities)
        {
            // // Since {Native}/{Romaji} is not a languageCode, we need to handle separately and first
            if (LanguageCodeHelper.IsNativeToken(languageCode))
            {
                var native = externalMetadata.Titles?.NativeTitle;
                if (!string.IsNullOrWhiteSpace(native))
                {
                    yield return (native.Trim(), LanguageCodeHelper.NativeToken);
                }
                continue;
            }

            if (LanguageCodeHelper.IsRomajiToken(languageCode))
            {
                var romaji = externalMetadata.Titles?.RomajiTitle;
                if (!string.IsNullOrWhiteSpace(romaji))
                {
                    yield return (romaji.Trim(), LanguageCodeHelper.RomajiToken);
                }
                continue;
            }

            if (!titlesByLanguage.TryGetValue(languageCode, out var titles)) continue;

            foreach (var title in titles)
            {
                if (string.IsNullOrWhiteSpace(title.Title)) continue;
                yield return (title.Title.Trim(), languageCode);
            }
        }
    }

    /// <summary>
    /// Finds which priority language code produced the name the Series currently holds, or null when none did.
    /// </summary>
    /// <remarks>
    /// Matching on the value rather than on our last write is what makes this correct across re-runs and for
    /// user-locked names: if the admin locked Name to something no provider language produces, nothing is
    /// excluded from the LocalizedName list and the value-level self checks still prevent a duplicate.
    /// </remarks>
    private static string? FindHeldLanguageCode(IReadOnlyList<string> priorities,
        ExternalSeriesDetailDto externalMetadata, string? normalizedName)
    {
        if (string.IsNullOrEmpty(normalizedName)) return null;

        foreach (var (title, languageCode) in EnumerateTitlesByPriority(priorities, externalMetadata))
        {
            if (title.ToNormalized() == normalizedName) return languageCode;
        }

        return null;
    }

    /// <summary>
    /// Writes the Series' visible Name from external metadata when enabled and not locked by the user.
    /// OriginalName remains the on-disk anchor, so this rename stays scan-safe. Walks down the admin's language
    /// priority list, skipping any candidate that would collide (normalized) with another series in the library+format.
    /// </summary>
    private async Task<(bool, MetadataFieldChangeDto?)> UpdateName(Series series, MetadataSettingsDto settings,
        ExternalSeriesDetailDto externalMetadata, IReadOnlyList<string> namePriority,
        IReadOnlySet<string> takenNames, CancellationToken ct)
    {
        if (!settings.EnableName) return (false, null);

        if (series.NameLocked && !HasForceOverride(settings, series.Metadata, MetadataSettingField.Name))
        {
            return (false, null);
        }

        string? chosen = null;
        string? chosenLanguageCode = null;

        foreach (var (title, languageCode) in EnumerateTitlesByPriority(namePriority, externalMetadata))
        {
            var normalized = title.ToNormalized();
            if (string.IsNullOrEmpty(normalized)) continue;

            // The best candidate is already our Name. Stop rather than continue - sliding to a lower-priority
            // language here would rename the series away from a title it correctly holds.
            if (normalized == series.NormalizedName) return (false, null);

            // Never create a normalized collision - it would make the scanner's SingleOrDefault lookup throw
            if (takenNames.Contains(normalized)) continue;

            chosen = title;
            chosenLanguageCode = languageCode;
            break;
        }

        if (chosen == null)
        {
            _logger.LogDebug(
                "[K+] Skipping name write for Series {SeriesId}: no unique candidate matched the configured languages",
                series.Id);
            return (false, null);
        }

        if (await WouldOrphanMergedFiles(series, series.Name, chosen, series.NormalizedLocalizedName, series.NormalizedOriginalName, ct))
        {
            _logger.LogDebug("[K+] Skipping name write for Series {SeriesId}: current name anchors merged files on disk", series.Id);
            return (false, null);
        }

        var from = series.Name;
        series.Name = chosen;
        series.NormalizedName = chosen.ToNormalized();
        series.SortName = series.Library is {RemovePrefixForSortName: true}
            ? BookSortTitlePrefixHelper.GetSortTitle(series.Name)
            : series.Name;

        series.NameLocked = true;
        series.Metadata.AddKPlusOverride(MetadataSettingField.Name);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.Name, from, series.Name, chosenLanguageCode));
    }

    /// <summary>
    /// Writes the Series' LocalizedName from external metadata, walking down the admin's language priority list
    /// and skipping any candidate that would collide with another series or with this Series' own name fields.
    /// </summary>
    /// <param name="heldNameLanguageCode">
    /// The language code Series.Name currently holds. Dropped from the priority list so the two fields never
    /// resolve from the same language.
    /// </param>
    private async Task<(bool, MetadataFieldChangeDto?)> UpdateLocalizedName(Series series, MetadataSettingsDto settings,
        ExternalSeriesDetailDto externalMetadata, IReadOnlyList<string> localizedNamePriority,
        string? heldNameLanguageCode, IReadOnlySet<string> takenNames, CancellationToken ct)
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

        // If Name took a language, LocalizedName can't have it
        if (!string.IsNullOrEmpty(heldNameLanguageCode))
        {
            localizedNamePriority = localizedNamePriority
                .Where(c => !string.Equals(c, heldNameLanguageCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        string? chosen = null;
        string? chosenLanguageCode = null;

        foreach (var (title, languageCode) in EnumerateTitlesByPriority(localizedNamePriority, externalMetadata))
        {
            var normalized = title.ToNormalized();
            if (string.IsNullOrEmpty(normalized)) continue;

            // A localized name that normalizes to another series' Name/LocalizedName/OriginalName in the same
            // library+format breaks the scanner
            if (takenNames.Contains(normalized)) continue;

            // takenNames excludes this Series, so our own columns need checking separately. Dropping the language
            // code above is not enough - two languages can carry the same title text (en and ja-Latn "Bleach").
            // series.NormalizedName is already the post-UpdateName value here.
            if (normalized == series.NormalizedName || normalized == series.NormalizedOriginalName) continue;

            chosen = title;
            chosenLanguageCode = languageCode;
            break;
        }

        if (chosen == null)
        {
            _logger.LogDebug(
                "[K+] Skipping localized name write for Series {SeriesId}: no unique candidate matched the configured languages",
                series.Id);
            return (false, null);
        }

        if (await WouldOrphanMergedFiles(series, series.LocalizedName, chosen, series.NormalizedName, series.NormalizedOriginalName, ct))
        {
            _logger.LogDebug("[K+] Skipping localized name write for Series {SeriesId}: current value anchors merged files on disk", series.Id);
            return (false, null);
        }

        var from = series.LocalizedName;
        series.LocalizedName = chosen;
        series.NormalizedLocalizedName = chosen.ToNormalized();
        series.LocalizedNameLocked = true;
        series.Metadata.AddKPlusOverride(MetadataSettingField.LocalizedName);

        return (true, new MetadataFieldChangeDto(MetadataFieldChangeKind.LocalizedName, from, series.LocalizedName, chosenLanguageCode));
    }

    /// <inheritdoc />
    public Task<bool> WouldNameChangeOrphanMergedFiles(Series series, string? proposedName, CancellationToken ct = default)
    {
        return WouldOrphanMergedFiles(series, series.Name, proposedName ?? string.Empty,
            series.NormalizedLocalizedName, series.NormalizedOriginalName, ct);
    }

    /// <inheritdoc />
    public Task<bool> WouldLocalizedNameChangeOrphanMergedFiles(Series series, string? proposedLocalizedName, CancellationToken ct = default)
    {
        return WouldOrphanMergedFiles(series, series.LocalizedName, proposedLocalizedName ?? string.Empty,
            series.NormalizedName, series.NormalizedOriginalName, ct);
    }

    /// <summary>
    /// Guards a K+ rename from orphaning merged files.
    /// <example>
    /// A folder literally named "Chained Soldier" is merged under Name "Mato Seihei no Slave" via
    /// LocalizedName "Chained Soldier". OriginalName only anchors "Mato Seihei no Slave", so if K+ overwrites
    /// LocalizedName the scanner stops matching the "Chained Soldier" folder and splits it into a new series.
    /// </example>
    /// Returns true when <paramref name="droppedName"/> still matches a folder on disk and is not covered by
    /// the other two name fields - so the write must be skipped even under a force override.
    /// </summary>
    private async Task<bool> WouldOrphanMergedFiles(Series series, string droppedName, string proposedName,
        string survivorA, string survivorB, CancellationToken ct)
    {
        var dropped = droppedName.ToNormalized();
        if (string.IsNullOrEmpty(dropped)) return false;
        if (dropped == proposedName.ToNormalized()) return false;       // not actually changing
        if (dropped == survivorA || dropped == survivorB) return false; // still held by Name/OriginalName

        var files = await _unitOfWork.SeriesRepository.GetFilesForSeriesAsync(series.Id, ct);
        return files
            .Select(f => Path.GetDirectoryName(f.FilePath))
            .Where(d => !string.IsNullOrEmpty(d))
            .SelectMany(d => d!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Any(segment => segment.ToNormalized() == dropped || Parser.CleanTitle(segment).ToNormalized() == dropped);
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
        series.Metadata.SummaryLocked = true;

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
            if (string.IsNullOrEmpty(staff.ImageUrl)) continue;

            var aniListId = ExternalIdParser.GetAniListStaffId(staff.Url);
            var hardcoverId = ExternalIdParser.GetHardcoverStaffId(staff.Url);

            if (aniListId > 0 && staff.ImageUrl.EndsWith("default.jpg")) continue;

            Person? person = null;

            if (aniListId > 0)
            {
                person = await _unitOfWork.PersonRepository.GetPersonByAniListId(aniListId);
            }

            if (person == null && !string.IsNullOrEmpty(hardcoverId))
            {
                person = await _unitOfWork.PersonRepository.GetPersonByHardcoverId(hardcoverId);
            }

            if (person == null|| !string.IsNullOrEmpty(person.CoverImage)) continue;

            try
            {
                await _coverDbService.SetPersonCoverByUrl(person, staff.ImageUrl, false, true);
                await _auditService.LogPersonAsync(KavitaPlusEventType.PersonCoverUpdated, person.Id,
                    new AuditLogPersonCoverParamsDto { PersonName = person.Name, AniListId = aniListId, HardcoverId = hardcoverId, ImageUrl = staff.ImageUrl });
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
            var realVolumes = series.Volumes
                .Where(v => v.MaxNumber.IsNot(Parser.SpecialVolumeNumber) && v.MaxNumber.IsNot(Parser.LooseLeafVolumeNumber))
                .ToList();

            var isVolumeBased = realVolumes.Count != 0;

            var maxVolume = (int)(realVolumes.Count != 0 ? realVolumes.Max(v => v.MaxNumber) : 0);
            var maxChapter = (int)chapters.Max(c => c.MaxNumber);

            var externalExpectedCount = isVolumeBased ? externalMetadata.Volumes : externalMetadata.Chapters;

            series.Metadata.TotalCount = Math.Max(
                chapters.Max(chapter => chapter.TotalCount),
                externalExpectedCount
            );

            series.Metadata.MaxCount = isVolumeBased ? maxVolume : maxChapter;

            if (series.Format is MangaFormat.Epub or MangaFormat.Pdf && chapters.Count == 1)
            {
                series.Metadata.MaxCount = 1;
            }
            else if (series.Metadata.TotalCount <= 1 && chapters is [{ IsSpecial: true }])
            {
                series.Metadata.MaxCount = series.Metadata.TotalCount;
            }

            var status = PublicationStatus.OnGoing;
            var hasExternalCounts = isVolumeBased ? externalMetadata.Volumes > 0 : externalMetadata.Chapters > 0;

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
        ExternalSeriesMetadata externalSeriesMetadata, RecommendationSource source, MetadataProvider provider,
        MetadataSettingsDto settings, ISet<string> seen)
    {
        var recDto = new RecommendationDto()
        {
            ExternalSeries = new List<ExternalSeriesDto>(),
            OwnedSeries = new List<RecommendedSeriesDto>()
        };

        // NOTE: This can result in a series being recommended that shares the same name but different format
        foreach (var rec in recs)
        {
            // Skip recommendations already added from a higher-priority list (e.g. Personalized before Similar)
            if (!seen.Add(GetRecommendationIdentity(rec))) continue;

            // Raise the provider's base rating via our own tag/genre mappings; fails closed when indeterminate
            var ageRating = RecommendationHelper.ComputeExternalAgeRating(rec.AgeRating, rec.Genres, rec.Tags, settings);

            // Find the series based on name and type and that the user has access too
            var seriesForRec = await _unitOfWork.SeriesRepository.GetSeriesDtoByNamesAndMetadataIdsAsync(rec.RecommendationNames,
                libraryType, rec);

            if (seriesForRec != null)
            {
                recDto.OwnedSeries.Add(new RecommendedSeriesDto() { Series = seriesForRec, Source = source });
                externalSeriesMetadata.ExternalRecommendations.Add(new ExternalRecommendation()
                {
                    SeriesId = seriesForRec.Id,
                    AniListId = rec.AniListId,
                    MalId = rec.MalId,
                    Name = seriesForRec.Name,
                    Url = rec.SiteUrl,
                    CoverUrl = rec.CoverUrl,
                    Summary = rec.Summary,
                    MangaBakaId = (int?) rec.MangabakaId,
                    MetadataProvider = provider,
                    RecommendationSource = source,
                    AgeRating = ageRating
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
                MalId = rec.MalId,
                MangaBakaId = (int?) rec.MangabakaId,
                MetadataProvider = provider,
                RecommendationSource = source,
                AgeRating = ageRating
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
                MangaBakaId = (int?) rec.MangabakaId,
                MetadataProvider = provider,
                RecommendationSource = source,
                AgeRating = ageRating
            });
        }

        recDto.OwnedSeries = recDto.OwnedSeries.DistinctBy(s => s.Series.Id).OrderBy(r => r.Series.Name).ToList();
        recDto.ExternalSeries = recDto.ExternalSeries.DistinctBy(s => s.Name.ToNormalized()).OrderBy(r => r.Name).ToList();

        return recDto;
    }

    /// <summary>
    /// Stable key for a recommendation so the same series is only added once across the Similar/Personalized lists,
    /// preferring whichever id is most navigable before falling back to the normalized name.
    /// </summary>
    private static string GetRecommendationIdentity(MediaRecommendationDto rec)
    {
        if (rec.MangabakaId is > 0) return $"mb:{rec.MangabakaId}";
        if (rec.AniListId is > 0) return $"al:{rec.AniListId}";
        if (rec.MalId is > 0) return $"mal:{rec.MalId}";

        var name = string.IsNullOrEmpty(rec.Name) ? rec.RecommendationNames.FirstOrDefault() : rec.Name;
        return $"name:{(name ?? string.Empty).ToNormalized()}";
    }


    /// <summary>
    /// This is to get series information for the recommendation drawer on Kavita
    /// </summary>
    /// <remarks>This uses a different API that series detail</remarks>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="mangaBakaId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task<ExternalSeriesDetailDto?> GetSeriesDetail(int? aniListId, long? malId, int? mangaBakaId, int? seriesId, CancellationToken ct = default)
    {
        // TODO: This is the primary point where we need to integrate ExternalIds since weblink parsing is already handled
        // TODO: Ensure when we set/update weblinks via API, we reparse and update external ids (if they are empty only)
        var payload = new SeriesDetailRequestV3Dto()
        {
            // We can hardcode this for now. But will need to load from Library setting once Hardcover providers
            // recommendations too
            Provider = MetadataProvider.Mangabaka,
            AniListId = aniListId,
            MalId = malId,
            MangabakaId = mangaBakaId,
            SeriesName = string.Empty,
            AlternativeNames = [],
            IncludeRecommendations = false,
            IncludeReviews = false,
            IncludeRelationships = false
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
                payload.AlternativeNames = [series.LocalizedName];
                payload.Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format);
            }
        }


        var result =  await _kavitaPlusApiService.GetSeriesDetailV3Async(payload, ct);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to retrieve series detail from Kavita Plus API: {ErrorMessage}", result.ErrorMessage);
            return null;
        }

        var extSeries = result.Data.Series;
        if (extSeries == null) return null;

        var settings = await _unitOfWork.SettingsRepository.GetMetadataSettingDto(ct);

        var genres = new List<string>();
        var tags = new List<string>();
        GenerateGenreAndTagLists(extSeries, settings, ref tags, ref genres);

        var tagsToRemove = GetTagsToRemove(extSeries, settings);
        var finalTags = tags.Except(tagsToRemove).ToHashSet();

        extSeries.Genres = genres;
        extSeries.Tags = extSeries.Tags.Where(t => finalTags.Contains(t.Name)).ToList();

        var ageRating = DetermineAgeRating(extSeries.Tags.Select(t => t.Name).Concat(extSeries.Genres), settings.AgeRatingMappings);

        extSeries.AgeRating = ageRating;
        extSeries.Summary = StringHelper.RemoveSourceInDescription(StringHelper.SquashBreaklines(extSeries.Summary));

        return extSeries;
    }

    private static bool HasForceOverride(MetadataSettingsDto settings, IHasKPlusMetadata kPlusMetadata,
        MetadataSettingField field)
    {
        return settings.HasOverride(field) || kPlusMetadata.HasSetKPlusMetadata(field);
    }
}
