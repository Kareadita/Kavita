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
    private readonly IMemoryCache _cache;

    public ReviewController(ILogger<ReviewController> logger, IUnitOfWork unitOfWork, ILicenseService licenseService,
        IMapper mapper, IReviewService reviewService, IMemoryCache cache)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _licenseService = licenseService;
        _mapper = mapper;
        _reviewService = reviewService;
        _cache = cache;
    }


    /// <summary>
    /// Fetches reviews from the server for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    [HttpGet]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Recommendation, VaryByQueryKeys = new []{"seriesId"})]
    public async Task<ActionResult<IEnumerable<UserReviewDto>>> GetReviews(int seriesId)
    {
        var userId = User.GetUserId();
        var userRatings = await _unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId, userId);
        if (!await _licenseService.DefaultUserHasLicense() || !await _licenseService.HasActiveLicense(userId))
        {
            return Ok(userRatings);
        }

        var cacheKey = "review-" + seriesId;
        IEnumerable<UserReviewDto> externalReviews;
        var setCache = false;
        if (_cache.TryGetValue(cacheKey, out string cachedData))
        {
            externalReviews = JsonConvert.DeserializeObject<IEnumerable<UserReviewDto>>(cachedData);
        }
        else
        {
            externalReviews = await _reviewService.GetReviewsForSeries(userId, seriesId);
            setCache = true;
        }

        // Fetch external reviews and splice them in
        foreach (var r in externalReviews)
        {
            userRatings.Add(r);
        }

        if (setCache)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(userRatings.Count)
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            _cache.Set(cacheKey, JsonConvert.SerializeObject(externalReviews), cacheEntryOptions);
        }

        return Ok(userRatings);
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
        return Ok(_mapper.Map<UserReviewDto>(rating));
    }
}
