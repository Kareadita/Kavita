using System;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IRatingService
{
    Task<bool> UpdateChapterRating(int userId, UpdateChapterRatingDto dto);
}

public class RatingService: IRatingService
{

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RatingService> _logger;

    public RatingService(IUnitOfWork unitOfWork, ILogger<RatingService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> UpdateChapterRating(int userId, UpdateChapterRatingDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.ChapterRatings);
        if (user == null) throw new UnauthorizedAccessException();

        var rating = await _unitOfWork.UserRepository.GetUserChapterRatingAsync(dto.ChapterId, userId) ?? new AppUserChapterRating();

        rating.Rating = Math.Clamp(dto.Rating, 0, 5);
        rating.HasBeenRated = true;
        rating.ChapterId = dto.ChapterId;

        if (rating.Id == 0)
        {
            user.ChapterRatings.Add(rating);
        }

        _unitOfWork.UserRepository.Update(user);

        try
        {
            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                // Scrobble Update?
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception while updating chapter rating");
        }

        await _unitOfWork.RollbackAsync();
        user.ChapterRatings.Remove(rating);
        return false;
    }
}
