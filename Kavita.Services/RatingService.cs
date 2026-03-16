using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class RatingService(IUnitOfWork unitOfWork, IScrobblingService scrobblingService, ILogger<RatingService> logger)
    : IRatingService
{
    public async Task<bool> UpdateSeriesRating(AppUser user, UpdateRatingDto updateRatingDto,
        CancellationToken ct = default)
    {
        var userRating =
            await unitOfWork.UserRepository.GetUserRatingAsync(updateRatingDto.SeriesId, user.Id, ct) ??
            new AppUserRating();

        try
        {
            userRating.Rating = Math.Clamp(updateRatingDto.UserRating, 0f, 5f);
            userRating.HasBeenRated = true;
            userRating.SeriesId = updateRatingDto.SeriesId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            unitOfWork.UserRepository.Update(user);

            if (!unitOfWork.HasChanges() || await unitOfWork.CommitAsync(ct))
            {
                BackgroundJob.Enqueue(() =>
                    scrobblingService.ScrobbleRatingUpdate(user.Id, updateRatingDto.SeriesId,
                        userRating.Rating));
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception saving rating");
        }

        await unitOfWork.RollbackAsync(ct);
        user.Ratings?.Remove(userRating);

        return false;
    }

    public async Task<bool> UpdateChapterRating(AppUser user, UpdateRatingDto updateRatingDto,
        CancellationToken ct = default)
    {
        if (updateRatingDto.ChapterId == null)
        {
            return false;
        }

        var userRating =
            await unitOfWork.UserRepository.GetUserChapterRatingAsync(user.Id, updateRatingDto.ChapterId.Value, ct) ??
            new AppUserChapterRating();

        try
        {
            userRating.Rating = Math.Clamp(updateRatingDto.UserRating, 0f, 5f);
            userRating.HasBeenRated = true;
            userRating.SeriesId = updateRatingDto.SeriesId;
            userRating.ChapterId = updateRatingDto.ChapterId.Value;

            if (userRating.Id == 0)
            {
                user.ChapterRatings ??= new List<AppUserChapterRating>();
                user.ChapterRatings.Add(userRating);
            }

            unitOfWork.UserRepository.Update(user);

            await unitOfWork.CommitAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception saving rating");
        }

        await unitOfWork.RollbackAsync(ct);
        user.ChapterRatings?.Remove(userRating);

        return false;
    }

}
