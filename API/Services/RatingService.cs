using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Services.Plus;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IRatingService
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="user">Should include ratings</param>
    /// <param name="updateRatingDto"></param>
    /// <returns></returns>
    Task<bool> UpdateRating(AppUser user, UpdateRatingDto updateRatingDto);
}

public class RatingService: IRatingService
{

    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILogger<RatingService> _logger;

    public RatingService(IUnitOfWork unitOfWork, IScrobblingService scrobblingService, ILogger<RatingService> logger)
    {
        _unitOfWork = unitOfWork;
        _scrobblingService = scrobblingService;
        _logger = logger;
    }

    public async Task<bool> UpdateRating(AppUser user, UpdateRatingDto updateRatingDto)
    {
        var userRating =
            await _unitOfWork.UserRepository.GetUserRatingAsync(updateRatingDto.SeriesId, user.Id, updateRatingDto.ChapterId) ??
            new AppUserRating();

        try
        {
            userRating.Rating = Math.Clamp(updateRatingDto.UserRating, 0f, 5f);
            userRating.HasBeenRated = true;
            userRating.SeriesId = updateRatingDto.SeriesId;
            userRating.ChapterId = updateRatingDto.ChapterId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                BackgroundJob.Enqueue(() =>
                    _scrobblingService.ScrobbleRatingUpdate(user.Id, updateRatingDto.SeriesId,
                        userRating.Rating));
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception saving rating");
        }

        await _unitOfWork.RollbackAsync();
        user.Ratings?.Remove(userRating);

        return false;
    }
}
