using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
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

    /// <summary>
    /// Updates the review for a given series
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpPost("chapter/{chapterId}")]
    public async Task<ActionResult<UserReviewDto>> UpdateChapterReview(int chapterId, UpdateUserReviewDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ChapterIncludes.None);
        if (chapter == null) return BadRequest();

        var builder = new ChapterRatingBuilder(user.ChapterRatings.FirstOrDefault(r => r.SeriesId == dto.SeriesId));

        var rating = builder
            .WithSeriesId(dto.SeriesId)
            .WithVolumeId(chapter.VolumeId)
            .WithChapterId(chapter.Id)
            .WithReview(dto.Body)
            .Build();

        if (rating.Id == 0)
        {
            user.ChapterRatings.Add(rating);
        }
        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        // Do I need this?
        //BackgroundJob.Enqueue(() =>
        //    _scrobblingService.ScrobbleReviewUpdate(user.Id, dto.SeriesId, string.Empty, dto.Body));
        return Ok(_mapper.Map<UserReviewDto>(rating));
    }


    /// <summary>
    /// Deletes the user's review for the given series
    /// </summary>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteReview(int seriesId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Ratings);
        if (user == null) return Unauthorized();

        user.Ratings = user.Ratings.Where(r => r.SeriesId != seriesId).ToList();

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Deletes the user's review for a given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpDelete("chapter/{chapterId}")]
    public async Task<IActionResult> DeleteChapterReview(int chapterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.ChapterRatings);
        if (user == null) return Unauthorized();

        user.ChapterRatings = user.ChapterRatings.Where(c => c.ChapterId != chapterId).ToList();

        _unitOfWork.UserRepository.Update(user);

        await _unitOfWork.CommitAsync();

        return Ok();
    }
}
