using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.SeriesDetail;
using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace API.Controllers;

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController : BaseApiController
{
    private readonly ILicenseService _licenseService;
    private readonly IRatingService _ratingService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RatingController> _logger;

    public RatingController(ILicenseService licenseService, IRatingService ratingService, IMemoryCache memoryCache, ILogger<RatingController> logger)
    {
        _licenseService = licenseService;
        _ratingService = ratingService;
        _cache = memoryCache;
        _logger = logger;
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

        var cacheKey = "rating-" + seriesId;
        var setCache = false;
        IEnumerable<RatingDto> ratings;
        if (_cache.TryGetValue(cacheKey, out string cachedData))
        {
            ratings = JsonConvert.DeserializeObject<IEnumerable<RatingDto>>(cachedData);
        }
        else
        {
            ratings = await _ratingService.GetRatings(seriesId);
            setCache = true;
        }

        if (setCache)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetAbsoluteExpiration(TimeSpan.FromHours(24));
            _cache.Set(cacheKey, JsonConvert.SerializeObject(ratings), cacheEntryOptions);
            _logger.LogDebug("Caching external rating for {Key}", cacheKey);
        }

        return Ok(ratings);
    }
}
