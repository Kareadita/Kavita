using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Extensions;
using API.Services;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRatingService _ratingService;
    private readonly ILocalizationService _localizationService;

    public RatingController(IUnitOfWork unitOfWork, IRatingService ratingService, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _ratingService = ratingService;
        _localizationService = localizationService;
    }

    [HttpPost]
    public async Task<ActionResult> UpdateRating(UpdateRatingDto updateRating)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        if (await _ratingService.UpdateRating(user, updateRating))
        {
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    [HttpGet("overall")]
    public async Task<ActionResult<RatingDto>> GetOverallRating(int seriesId, [FromQuery] int? chapterId)
    {
        int average;
        if (chapterId != null)
        {
            average = await _unitOfWork.ChapterRepository.GetAverageUserRating(chapterId.Value, User.GetUserId());
        }
        else
        {
            average = await _unitOfWork.SeriesRepository.GetAverageUserRating(seriesId, User.GetUserId());
        }


        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = average,
            FavoriteCount = 0,
        });
    }
}
