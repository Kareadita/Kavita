using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using AutoMapper;
using EasyCaching.Core;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace API.Controllers;

public class ReviewController : BaseApiController
{
    private readonly ILogger<ReviewController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILicenseService _licenseService;
    private readonly IMapper _mapper;
    private readonly IReviewService _reviewService;
    private readonly IScrobblingService _scrobblingService;
    private readonly IEasyCachingProvider _cacheProvider;
    public const string CacheKey = "review_";

    public ReviewController(ILogger<ReviewController> logger, IUnitOfWork unitOfWork, ILicenseService licenseService,
        IMapper mapper, IReviewService reviewService, IScrobblingService scrobblingService,
        IEasyCachingProviderFactory cachingProviderFactory)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _licenseService = licenseService;
        _mapper = mapper;
        _reviewService = reviewService;
        _scrobblingService = scrobblingService;

        _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusReviews);
    }


    /// <summary>
    /// Fetches reviews from the server for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    [HttpGet]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.KavitaPlus, VaryByQueryKeys = new []{"seriesId"})]
    public async Task<ActionResult<IEnumerable<UserReviewDto>>> GetReviews(int seriesId)
    {
        var userId = User.GetUserId();
        var userRatings = (await _unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId, userId))
            .Where(r => !string.IsNullOrEmpty(r.Body) && !string.IsNullOrEmpty(r.Tagline))
            .ToList();
        if (!await _licenseService.HasActiveLicense())
        {
            return Ok(userRatings);
        }

        var cacheKey = CacheKey + seriesId;
        IEnumerable<UserReviewDto> externalReviews;
        var setCache = false;

        var result = await _cacheProvider.GetAsync<IEnumerable<UserReviewDto>>(cacheKey);
        if (result.HasValue)
        {
            externalReviews = result.Value;
        }
        else
        {
            externalReviews = await _reviewService.GetReviewsForSeries(userId, seriesId);
            setCache = true;
        }
        // if (_cache.TryGetValue(cacheKey, out string cachedData))
        // {
        //     externalReviews = JsonConvert.DeserializeObject<IEnumerable<UserReviewDto>>(cachedData);
        // }
        // else
        // {
        //     externalReviews = await _reviewService.GetReviewsForSeries(userId, seriesId);
        //     setCache = true;
        // }

        // Fetch external reviews and splice them in
        foreach (var r in externalReviews)
        {
            userRatings.Add(r);
        }

        if (setCache)
        {
            // var cacheEntryOptions = new MemoryCacheEntryOptions()
            //     .SetSize(userRatings.Count)
            //     .SetAbsoluteExpiration(TimeSpan.FromHours(10));
            //_cache.Set(cacheKey, JsonConvert.SerializeObject(externalReviews), cacheEntryOptions);
            await _cacheProvider.SetAsync(cacheKey, externalReviews, TimeSpan.FromHours(10));
            _logger.LogDebug("Caching external reviews for {Key}", cacheKey);
        }

        return Ok(userRatings.Take(10));
    }

    /// <summary>
    /// Updates the review for a given series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<UserReviewDto>> UpdateReview(UpdateUserReviewDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        var ratingBuilder = new RatingBuilder(user.Ratings.FirstOrDefault(r => r.SeriesId == dto.SeriesId));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .WithTagline(dto.Tagline)
            .Build();

        if (rating.Id == 0)
        {
            user.Ratings.Add(rating);
        }
        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();


        BackgroundJob.Enqueue(() =>
            _scrobblingService.ScrobbleReviewUpdate(user.Id, dto.SeriesId, dto.Tagline, dto.Body));
        return Ok(_mapper.Map<UserReviewDto>(rating));
    }
}
