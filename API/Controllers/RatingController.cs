using System;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Services;
using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Update the users' rating of the given series
    /// </summary>
    /// <param name="updateRating"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [HttpPost("series")]
    public async Task<ActionResult> UpdateSeriesRating(UpdateRatingDto updateRating)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        if (await _ratingService.UpdateSeriesRating(user, updateRating))
        {
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-error"));
    }

    /// <summary>
    /// Update the users' rating of the given chapter
    /// </summary>
    /// <param name="updateRating">chapterId must be set</param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [HttpPost("chapter")]
    public async Task<ActionResult> UpdateChapterRating(UpdateRatingDto updateRating)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        if (await _ratingService.UpdateChapterRating(user, updateRating))
        {
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-error"));
    }

    /// <summary>
    /// Overall rating from all Kavita users for a given Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("overall-series")]
    public async Task<ActionResult<RatingDto>> GetOverallSeriesRating(int seriesId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await _unitOfWork.SeriesRepository.GetAverageUserRating(seriesId, UserId),
            FavoriteCount = 0,
        });
    }

    /// <summary>
    /// Overall rating from all Kavita users for a given Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("overall-chapter")]
    public async Task<ActionResult<RatingDto>> GetOverallChapterRating(int chapterId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await _unitOfWork.ChapterRepository.GetAverageUserRating(chapterId, UserId),
            FavoriteCount = 0,
        });
    }
}
