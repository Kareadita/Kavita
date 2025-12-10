using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
using API.Helpers.Builders;
using API.Services.Plus;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

public class ReviewController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IScrobblingService _scrobblingService;

    public ReviewController(IUnitOfWork unitOfWork,
        IMapper mapper, IScrobblingService scrobblingService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _scrobblingService = scrobblingService;
    }


    /// <summary>
    /// Updates the user's review for a given series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("series")]
    public async Task<ActionResult<UserReviewDto>> UpdateSeriesReview(UpdateUserReviewDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        var ratingBuilder = new RatingBuilder(await _unitOfWork.UserRepository.GetUserRatingAsync(dto.SeriesId, user.Id));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
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

    /// <summary>
    /// Update the user's review for a given chapter
    /// </summary>
    /// <param name="dto">chapterId must be set</param>
    /// <returns></returns>
    [HttpPost("chapter")]
    public async Task<ActionResult<UserReviewDto>> UpdateChapterReview(UpdateUserReviewDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        if (dto.ChapterId == null) return BadRequest();

        int chapterId = dto.ChapterId.Value;

        var ratingBuilder = new ChapterRatingBuilder(await _unitOfWork.UserRepository.GetUserChapterRatingAsync(user.Id, chapterId));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .WithChapterId(chapterId)
            .Build();

        if (rating.Id == 0)
        {
            user.ChapterRatings.Add(rating);
        }

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok(_mapper.Map<UserReviewDto>(rating));
    }


    /// <summary>
    /// Deletes the user's review for the given series
    /// </summary>
    /// <returns></returns>
    [HttpDelete("series")]
    public async Task<ActionResult> DeleteSeriesReview([FromQuery] int seriesId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        user.Ratings = user.Ratings.Where(r => r.SeriesId != seriesId).ToList();

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Deletes the user's review for the given chapter
    /// </summary>
    /// <returns></returns>
    [HttpDelete("chapter")]
    public async Task<ActionResult> DeleteChapterReview([FromQuery] int chapterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        user.ChapterRatings = user.ChapterRatings.Where(r => r.ChapterId != chapterId).ToList();

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

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
        return Ok(await _unitOfWork.UserRepository.GetAllReviewsForUser(userId, UserId, filterQuery, rating));
    }
}
