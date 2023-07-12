using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Services.Plus;
using EasyCaching.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController : BaseApiController
{
    private readonly ILicenseService _licenseService;
    private readonly IRatingService _ratingService;
    private readonly ILogger<RatingController> _logger;
    private readonly IEasyCachingProvider _cacheProvider;
    public const string CacheKey = "rating_";

    public RatingController(ILicenseService licenseService, IRatingService ratingService,
        ILogger<RatingController> logger, IEasyCachingProviderFactory cachingProviderFactory)
    {
        _licenseService = licenseService;
        _ratingService = ratingService;
        _logger = logger;

        _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusRatings);
    }

    /// <summary>
    /// Get the external ratings for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Recommendation, VaryByQueryKeys = new []{"seriesId"})]
    public async Task<ActionResult<IEnumerable<RatingDto>>> GetRating(int seriesId)
    {
        if (!await _licenseService.HasActiveLicense())
        {
            return Ok(new List<RatingDto>());
        }

        var cacheKey = CacheKey + seriesId;
        var results = await _cacheProvider.GetAsync<IEnumerable<RatingDto>>(cacheKey);
        if (results.HasValue)
        {
            return Ok(results.Value);
        }

        var ratings = await _ratingService.GetRatings(seriesId);
        await _cacheProvider.SetAsync(cacheKey, ratings, TimeSpan.FromHours(24));
        _logger.LogDebug("Caching external rating for {Key}", cacheKey);
        return Ok(ratings);

    }
}
