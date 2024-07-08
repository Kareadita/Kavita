using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Collection;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using AutoMapper;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

/// <summary>
/// Used for matching and fetching metadata on a series
/// </summary>
internal class ExternalMetadataIdsDto
{
    public long? MalId { get; set; }
    public int? AniListId { get; set; }

    public string? SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    public MediaFormat? PlusMediaFormat { get; set; } = MediaFormat.Unknown;
}

internal class SeriesDetailPlusApiDto
{
    public IEnumerable<MediaRecommendationDto> Recommendations { get; set; }
    public IEnumerable<UserReviewDto> Reviews { get; set; }
    public IEnumerable<RatingDto> Ratings { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
}

public interface IExternalMetadataService
{
    Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId);
    Task<SeriesDetailPlusDto> GetSeriesDetailPlus(int seriesId, LibraryType libraryType);
    Task ForceKavitaPlusRefresh(int seriesId);
    Task FetchExternalDataTask();
    /// <summary>
    /// This is an entry point and provides a level of protection against calling upstream API. Will only allow 100 new
    /// series to fetch data within a day and enqueues background jobs at certain times to fetch that data.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <returns></returns>
    Task GetNewSeriesData(int seriesId, LibraryType libraryType);

    Task<IList<MalStackDto>> GetStacksForUser(int userId);
}

public class ExternalMetadataService : IExternalMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExternalMetadataService> _logger;
    private readonly IMapper _mapper;
    private readonly ILicenseService _licenseService;
    private readonly TimeSpan _externalSeriesMetadataCache = TimeSpan.FromDays(30);
    public static readonly ImmutableArray<LibraryType> NonEligibleLibraryTypes = ImmutableArray.Create
        (LibraryType.Comic, LibraryType.Book, LibraryType.Image, LibraryType.ComicVine);
    private readonly SeriesDetailPlusDto _defaultReturn = new()
    {
        Recommendations = null,
        Ratings = ArraySegment<RatingDto>.Empty,
        Reviews = ArraySegment<UserReviewDto>.Empty
    };
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);

    public ExternalMetadataService(IUnitOfWork unitOfWork, ILogger<ExternalMetadataService> logger, IMapper mapper, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _licenseService = licenseService;


        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
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
    /// <remarks>To avoid blasting Kavita+ API, this only processes a few records. The goal is to slowly build </remarks>
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
            await GetNewSeriesData(seriesId, libraryType);
            await Task.Delay(1500);
            count++;
        }
        _logger.LogInformation("[Kavita+ Data Refresh] Finished Refreshing {Count} series data from Kavita+", count);
    }

    /// <summary>
    /// Removes from Blacklist and Invalidates the cache
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task ForceKavitaPlusRefresh(int seriesId)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeBySeriesIdAsync(seriesId);
        if (!IsPlusEligible(libraryType)) return;

        // Remove from Blacklist if applicable
        await _unitOfWork.ExternalSeriesMetadataRepository.RemoveFromBlacklist(seriesId);

        var metadata = await _unitOfWork.ExternalSeriesMetadataRepository.GetExternalSeriesMetadata(seriesId);
        if (metadata == null) return;

        metadata.ValidUntilUtc = DateTime.UtcNow.Subtract(_externalSeriesMetadataCache);
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Fetches data from Kavita+
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    public async Task GetNewSeriesData(int seriesId, LibraryType libraryType)
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

        // TODO: Fetch Series Metadata (Summary, etc)

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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
    /// <returns></returns>
    public async Task<SeriesDetailPlusDto> GetSeriesDetailPlus(int seriesId, LibraryType libraryType)
    {
        if (!IsPlusEligible(libraryType) || !await _licenseService.HasActiveLicense()) return _defaultReturn;

        // Check blacklist (bad matches)
        if (await _unitOfWork.ExternalSeriesMetadataRepository.IsBlacklistedSeries(seriesId)) return _defaultReturn;

        var needsRefresh =
            await _unitOfWork.ExternalSeriesMetadataRepository.ExternalSeriesMetadataNeedsRefresh(seriesId);

        if (!needsRefresh)
        {
            // Convert into DTOs and return
            return await _unitOfWork.ExternalSeriesMetadataRepository.GetSeriesDetailPlusDto(seriesId);
        }

        try
        {
            var data = await _unitOfWork.SeriesRepository.GetPlusSeriesDto(seriesId);
            if (data == null) return _defaultReturn;
            _logger.LogDebug("Fetching Kavita+ Series Detail data for {SeriesName}", data.SeriesName);

            var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
            var result = await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-detail")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(data)
                .ReceiveJson<SeriesDetailPlusApiDto>();


            // Clear out existing results
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            var externalSeriesMetadata = await GetExternalSeriesMetadataForSeries(seriesId, series!);
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
            await _unitOfWork.CommitAsync();

            return new SeriesDetailPlusDto()
            {
                Recommendations = recs,
                Ratings = result.Ratings,
                Reviews = externalSeriesMetadata.ExternalReviews.Select(r => _mapper.Map<UserReviewDto>(r))
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
            _logger.LogError(ex, "An error happened during the request to Kavita+ API");
        }

        // Blacklist the series as it wasn't found in Kavita+
        await _unitOfWork.ExternalSeriesMetadataRepository.CreateBlacklistedSeries(seriesId);

        return _defaultReturn;
    }


    private async Task<ExternalSeriesMetadata> GetExternalSeriesMetadataForSeries(int seriesId, Series series)
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
                payload.PlusMediaFormat = ConvertToMediaFormat(series.Library.Type, series.Format);
            }

        }
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-by-ids")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(payload)
                .ReceiveJson<ExternalSeriesDetailDto>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    private static MediaFormat ConvertToMediaFormat(LibraryType libraryType, MangaFormat seriesFormat)
    {
        return libraryType switch
        {
            LibraryType.Manga => seriesFormat == MangaFormat.Epub ? MediaFormat.LightNovel : MediaFormat.Manga,
            LibraryType.Comic => MediaFormat.Comic,
            LibraryType.Book => MediaFormat.Book,
            LibraryType.LightNovel => MediaFormat.LightNovel,
            _ => MediaFormat.Unknown
        };
    }
}
