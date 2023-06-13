using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class ReviewController : BaseApiController
{
    private readonly ILogger<ReviewController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILicenseService _licenseService;
    private readonly IMapper _mapper;
    private readonly IReviewService _reviewService;

    public ReviewController(ILogger<ReviewController> logger, IUnitOfWork unitOfWork, ILicenseService licenseService,
        IMapper mapper, IReviewService reviewService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _licenseService = licenseService;
        _mapper = mapper;
        _reviewService = reviewService;
    }


    /// <summary>
    /// Fetches reviews from the server for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserReviewDto>>> GetReviews(int seriesId)
    {
        var userRatings = await _unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId);
        if (!await _licenseService.DefaultUserHasLicense() || !await _licenseService.HasActiveLicense(User.GetUserId()))
        {
            return Ok(userRatings);
        }

        // Fetch external reviews and splice them in
        var externalReviews = await _reviewService.GetReviewsForSeries(User.GetUserId(), seriesId);
        foreach (var r in externalReviews)
        {
            userRatings.Add(r);
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
