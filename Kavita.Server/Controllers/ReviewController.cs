using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

public class ReviewController(
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IScrobblingService scrobblingService)
    : BaseApiController
{
    /// <summary>
    /// Updates the user's review for a given series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<UserReviewDto>> UpdateSeriesReview(UpdateUserReviewDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, dto.SeriesId))
            return NotFound();

        var ratingBuilder = new RatingBuilder(await unitOfWork.UserRepository.GetUserRatingAsync(dto.SeriesId, user.Id));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .Build();

        if (rating.Id == 0)
        {
            user.Ratings.Add(rating);
        }

        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        BackgroundJob.Enqueue(() =>
            scrobblingService.ScrobbleReviewUpdate(user.Id, dto.SeriesId, string.Empty, dto.Body));
        return Ok(mapper.Map<UserReviewDto>(rating));
    }

    /// <summary>
    /// Update the user's review for a given chapter
    /// </summary>
    /// <param name="dto">chapterId must be set</param>
    /// <returns></returns>
    [HttpPost("chapter")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<UserReviewDto>> UpdateChapterReview(UpdateUserReviewDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        if (dto.ChapterId == null) return BadRequest();

        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, dto.SeriesId))
            return NotFound();

        var chapterId = dto.ChapterId.Value;

        var ratingBuilder = new ChapterRatingBuilder(await unitOfWork.UserRepository.GetUserChapterRatingAsync(user.Id, chapterId));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .WithChapterId(chapterId)
            .Build();

        if (rating.Id == 0)
        {
            user.ChapterRatings.Add(rating);
        }

        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        return Ok(mapper.Map<UserReviewDto>(rating));
    }


    /// <summary>
    /// Deletes the user's review for the given series
    /// </summary>
    /// <returns></returns>
    [HttpDelete("series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteSeriesReview([FromQuery] int seriesId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        user.Ratings = user.Ratings.Where(r => r.SeriesId != seriesId).ToList();

        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Deletes the user's review for the given chapter
    /// </summary>
    /// <returns></returns>
    [HttpDelete("chapter")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteChapterReview([FromQuery] int chapterId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        user.ChapterRatings = user.ChapterRatings.Where(r => r.ChapterId != chapterId).ToList();

        unitOfWork.UserRepository.Update(user);

        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Returns all reviews for the user. If you are authenticated as the user, will always return data, regardless of ShareReviews setting
    /// </summary>
    /// <param name="userId">User to load, if your own, will bypass RBS and ShareReviews restrictions</param>
    /// <param name="rating">Null to ignore filtering. >= rating</param>
    /// <param name="filterQuery">Null to ignore filtering on Series name</param>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<ActionResult<IList<UserReviewExtendedDto>>> GetAllReviewsForUser(int userId, float? rating = null, string? filterQuery = null)
    {
        return Ok(await unitOfWork.UserRepository.GetAllReviewsForUser(userId, UserId, filterQuery, rating));
    }
}
