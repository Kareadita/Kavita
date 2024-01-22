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
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class ReviewController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IReviewService _reviewService;
    private readonly IScrobblingService _scrobblingService;
    public const string CacheKey = "review_";

    public ReviewController(IUnitOfWork unitOfWork,
        IMapper mapper, IReviewService reviewService, IScrobblingService scrobblingService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _reviewService = reviewService;
        _scrobblingService = scrobblingService;
    }


    /// <summary>
    /// Fetches reviews from the server for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    [HttpGet]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.KavitaPlus, VaryByQueryKeys = ["seriesId"])]
    public async Task<ActionResult<IEnumerable<UserReviewDto>>> GetReviews(int seriesId)
    {
        return Ok(await _reviewService.GetReviewsForSeries(User.GetUserId(), seriesId));
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
            .WithTagline(string.Empty)
            .Build();

        if (rating.Id == 0)
        {
            user.Ratings.Add(rating);
        }
        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();


        BackgroundJob.Enqueue(() =>
            _scrobblingService.ScrobbleReviewUpdate(user.Id, dto.SeriesId, string.Empty, dto.Body));
        return Ok(_mapper.Map<UserReviewDto>(rating));
    }
}
