using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Services.Plus;
using EasyCaching.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController : BaseApiController
{
    private readonly ILicenseService _licenseService;
    private readonly IRatingService _ratingService;
    private readonly ILogger<RatingController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEasyCachingProvider _cacheProvider;
    public const string CacheKey = "rating_";

    public RatingController(ILicenseService licenseService, IRatingService ratingService,
        ILogger<RatingController> logger, IEasyCachingProviderFactory cachingProviderFactory, IUnitOfWork unitOfWork)
    {
        _licenseService = licenseService;
        _ratingService = ratingService;
        _logger = logger;
        _unitOfWork = unitOfWork;

        _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusRatings);
    }

    /// <summary>
    /// Get the external ratings for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.KavitaPlus, VaryByQueryKeys = new []{"seriesId"})]
    public async Task<ActionResult<IEnumerable<RatingDto>>> GetRating(int seriesId)
    {
        if (!await _licenseService.HasActiveLicense())
        {
            return Ok(Enumerable.Empty<RatingDto>());
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

    [HttpGet("overall")]
    public async Task<ActionResult<RatingDto>> GetOverallRating(int seriesId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await _unitOfWork.SeriesRepository.GetAverageUserRating(seriesId, User.GetUserId()),
            FavoriteCount = 0
        });
    }
}
