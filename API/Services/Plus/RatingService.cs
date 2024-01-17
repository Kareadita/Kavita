using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using EasyCaching.Core;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

public interface IRatingService
{
    Task<IEnumerable<RatingDto>> GetRatings(int seriesId);
}

public class RatingService : IRatingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RatingService> _logger;
    private readonly IEasyCachingProvider _cacheProvider;

    public const string CacheKey = "rating_";

    public RatingService(IUnitOfWork unitOfWork, ILogger<RatingService> logger, IEasyCachingProviderFactory cachingProviderFactory)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());

        _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusRatings);
    }

    /// <summary>
    /// Fetches Ratings for a given Series. Will check cache first
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<RatingDto>> GetRatings(int seriesId)
    {
        var cacheKey = CacheKey + seriesId;
        var results = await _cacheProvider.GetAsync<IEnumerable<RatingDto>>(cacheKey);
        if (results.HasValue)
        {
            return results.Value;
        }

        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
            SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.Chapters | SeriesIncludes.Volumes);

        // Don't send any ratings back for Comic libraries as Kavita+ doesn't have any providers for that
        if (series == null || series.Library.Type == LibraryType.Comic)
        {
            await _cacheProvider.SetAsync(cacheKey, ImmutableList<RatingDto>.Empty, TimeSpan.FromHours(24));
            return ImmutableList<RatingDto>.Empty;
        }

        var ratings = (await GetRatings(license.Value, series)).ToList();
        await _cacheProvider.SetAsync(cacheKey, ratings, TimeSpan.FromHours(24));
        _logger.LogDebug("Caching external rating for {Key}", cacheKey);

        return ratings;
    }

    private async Task<IEnumerable<RatingDto>> GetRatings(string license, Series series)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/rating")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(new PlusSeriesDtoBuilder(series).Build())
                .ReceiveJson<IEnumerable<RatingDto>>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return new List<RatingDto>();
    }
}
