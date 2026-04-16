using System;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController(
    IUnitOfWork unitOfWork,
    IRatingService ratingService,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Update the users' rating of the given series
    /// </summary>
    /// <param name="updateRating"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [HttpPost("series")]
    public async Task<ActionResult> UpdateSeriesRating(UpdateRatingDto updateRating)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, updateRating.SeriesId))
            return NotFound();

        if (await ratingService.UpdateSeriesRating(user, updateRating))
        {
            return Ok();
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
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
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, updateRating.SeriesId))
            return NotFound();

        if (await ratingService.UpdateChapterRating(user, updateRating))
        {
            return Ok();
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
    }

    /// <summary>
    /// Overall rating from all Kavita users for a given Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("overall-series")]
    public async Task<ActionResult<RatingDto>> GetOverallSeriesRating(int seriesId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await unitOfWork.SeriesRepository.GetAverageUserRatingAsync(seriesId, UserId),
            FavoriteCount = 0,
        });
    }

    /// <summary>
    /// Overall rating from all Kavita users for a given Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("overall-chapter")]
    public async Task<ActionResult<RatingDto>> GetOverallChapterRating(int chapterId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await unitOfWork.ChapterRepository.GetAverageUserRating(chapterId, UserId),
            FavoriteCount = 0,
        });
    }
}
