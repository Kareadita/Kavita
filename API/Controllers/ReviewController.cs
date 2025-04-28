using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
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
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        var ratingBuilder = new RatingBuilder(await _unitOfWork.UserRepository.GetUserRatingAsync(dto.SeriesId, user.Id, dto.ChapterId));

        var rating = ratingBuilder
            .WithBody(dto.Body)
            .WithSeriesId(dto.SeriesId)
            .WithChapterId(dto.ChapterId)
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


    /// <summary>
    /// Deletes the user's review for the given series, or chapter
    /// </summary>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteReview([FromQuery] int seriesId, [FromQuery] int? chapterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        user.Ratings = user.Ratings.Where(r => !(r.SeriesId == seriesId && r.ChapterId == chapterId)).ToList();

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok();
    }
}
