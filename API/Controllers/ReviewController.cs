using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
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
    /// Updates the review for a given series, or chapter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<UserReviewDto>> UpdateReview(UpdateUserReviewDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings | AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        UserReviewDto review;
        if (dto.ChapterId != null)
        {
            review = await UpdateChapterReview(user, dto, dto.ChapterId.Value);
        }
        else
        {
            review = await UpdateSeriesReview(user, dto);
        }
        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        if (dto.ChapterId == null)
        {
            BackgroundJob.Enqueue(() =>
                _scrobblingService.ScrobbleReviewUpdate(user.Id, dto.SeriesId, string.Empty, dto.Body));
        }
        return Ok(review);
    }

    private async Task<UserReviewDto> UpdateSeriesReview(AppUser user, UpdateUserReviewDto dto)
    {
        var ratingBuilder = new RatingBuilder(await _unitOfWork.UserRepository.GetUserRatingAsync(dto.SeriesId, user.Id));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .WithTagline(string.Empty)
            .Build();

        if (rating.Id == 0)
        {
            user.Ratings.Add(rating);
        }
        return _mapper.Map<UserReviewDto>(rating);
    }

    private async Task<UserReviewDto> UpdateChapterReview(AppUser user, UpdateUserReviewDto dto, int chapterId)
    {
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
        return _mapper.Map<UserReviewDto>(rating);
    }


    /// <summary>
    /// Deletes the user's review for the given series, or chapter
    /// </summary>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteReview([FromQuery] int seriesId, [FromQuery] int? chapterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        if (chapterId != null)
        {
            user.ChapterRatings = user.ChapterRatings.Where(r => r.ChapterId != chapterId).ToList();
        }
        else
        {
            user.Ratings = user.Ratings.Where(r => r.SeriesId != seriesId).ToList();
        }

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok();
    }
}
