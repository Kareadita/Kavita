﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Account;
using API.DTOs.Scrobbling;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Plus;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;


public class ScrobblingController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobblingService _scrobblingService;

    public ScrobblingController(IUnitOfWork unitOfWork, IScrobblingService scrobblingService)
    {
        _unitOfWork = unitOfWork;
        _scrobblingService = scrobblingService;
    }

    [HttpGet("anilist-token")]
    public async Task<ActionResult> GetAniListToken()
    {
        // Validate the license

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        return Ok(user.AniListAccessToken);
    }

    [HttpPost("update-anilist-token")]
    public async Task<ActionResult> UpdateAniListToken(AniListUpdateDto dto)
    {
        // Validate the license

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        var isNewToken = string.IsNullOrEmpty(user.AniListAccessToken);
        user.AniListAccessToken = dto.Token;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        if (isNewToken)
        {
            BackgroundJob.Enqueue(() => _scrobblingService.CreateEventsFromExistingHistory(user.Id));
        }

        return Ok();
    }

    [HttpGet("token-expired")]
    public async Task<ActionResult<bool>> HasTokenExpired(ScrobbleProvider provider)
    {
        return Ok(await _scrobblingService.HasTokenExpired(User.GetUserId(), provider));
    }

    /// <summary>
    /// Returns all scrobbling errors for the instance
    /// </summary>
    /// <remarks>Requires admin</remarks>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("scrobble-errors")]
    public async Task<ActionResult<IEnumerable<ScrobbleErrorDto>>> GetScrobbleErrors()
    {
        return Ok(await _unitOfWork.ScrobbleRepository.GetScrobbleErrors());
    }

    /// <summary>
    /// Clears the scrobbling errors table
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("clear-errors")]
    public async Task<ActionResult> ClearScrobbleErrors()
    {
        await _unitOfWork.ScrobbleRepository.ClearScrobbleErrors();
        return Ok();
    }

    /// <summary>
    /// Returns the scrobbling history for the user
    /// </summary>
    /// <remarks>User must have a valid license</remarks>
    /// <returns></returns>
    [HttpGet("scrobble-events")]
    public async Task<ActionResult<IEnumerable<ScrobbleEventDto>>> GetScrobblingEvents()
    {
        var events = await _unitOfWork.ScrobbleRepository.GetUserEvents(User.GetUserId());
        return Ok(events.OrderByDescending(e => e.LastModifiedUtc));
    }

    /// <summary>
    /// Returns all scrobble holds for the current user
    /// </summary>
    /// <returns></returns>
    [HttpGet("holds")]
    public async Task<ActionResult<IEnumerable<ScrobbleHoldDto>>> GetScrobbleHolds()
    {
        return Ok(await _unitOfWork.UserRepository.GetHolds(User.GetUserId()));
    }

    /// <summary>
    /// If there is an active hold on the series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("has-hold")]
    public async Task<ActionResult<bool>> HasHold(int seriesId)
    {
        return Ok(await _unitOfWork.UserRepository.HasHoldOnSeries(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Does the library the series is in allow scrobbling?
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("library-allows-scrobbling")]
    public async Task<ActionResult<bool>> LibraryAllowsScrobbling(int seriesId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library);
        return Ok(series != null && series.Library.AllowScrobbling);
    }

    /// <summary>
    /// Adds a hold against the Series for user's scrobbling
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("add-hold")]
    public async Task<ActionResult> AddHold(int seriesId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.ScrobbleHolds);
        if (user == null) return Unauthorized();
        if (user.ScrobbleHolds.Any(s => s.SeriesId == seriesId)) return Ok("Nothing to do");

        var seriesHold = new ScrobbleHoldBuilder().WithSeriesId(seriesId).Build();
        user.ScrobbleHolds.Add(seriesHold);
        _unitOfWork.UserRepository.Update(user);
        try
        {
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();
            return Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                // Reload the entity from the database
                await entry.ReloadAsync();
            }

            // Retry the update
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            // Handle other exceptions or log the error
            //_logger.LogError(ex, "An error occurred while adding the hold");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while adding the hold");
        }
    }

    /// <summary>
    /// Adds a hold against the Series for user's scrobbling
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpDelete("remove-hold")]
    public async Task<ActionResult> RemoveHold(int seriesId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.ScrobbleHolds);
        if (user == null) return Unauthorized();

        user.ScrobbleHolds = user.ScrobbleHolds.Where(h => h.SeriesId != seriesId).ToList();

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        return Ok();
    }
}
