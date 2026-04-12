using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common.Helpers;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.Account;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Scrobble;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class ScrobblingController(
    IUnitOfWork unitOfWork,
    IScrobblingService scrobblingService,
    ILogger<ScrobblingController> logger,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Get the current user's AniList token
    /// </summary>
    /// <returns></returns>
    [HttpGet("anilist-token")]
    public async Task<ActionResult<string>> GetAniListToken()
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user == null) return Unauthorized();

        return Ok(user.AniListAccessToken);
    }

    /// <summary>
    /// Get the current user's MAL token and username
    /// </summary>
    /// <returns></returns>
    [HttpGet("mal-token")]
    public async Task<ActionResult<MalUserInfoDto>> GetMalToken()
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user == null) return Unauthorized();

        return Ok(new MalUserInfoDto()
        {
            Username = user.MalUserName,
            AccessToken = user.MalAccessToken
        });
    }

    /// <summary>
    /// Update the current user's AniList token
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>True if the token was new or not</returns>
    [HttpPost("update-anilist-token")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> UpdateAniListToken(AniListUpdateDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user == null) return Unauthorized();

        var isNewToken = string.IsNullOrEmpty(user.AniListAccessToken);
        user.AniListAccessToken = dto.Token;
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        return Ok(isNewToken);
    }

    /// <summary>
    /// Update the current user's MAL token (Client ID) and Username
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>True if the token was new or not</returns>
    [HttpPost("update-mal-token")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> UpdateMalToken(MalUserInfoDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user == null) return Unauthorized();

        var isNewToken = string.IsNullOrEmpty(user.MalAccessToken);
        user.MalAccessToken = dto.AccessToken;
        user.MalUserName = dto.Username;

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        return Ok(isNewToken);
    }

    /// <summary>
    /// When a user request to generate scrobble events from history. Should only be ran once per user.
    /// </summary>
    /// <returns></returns>
    [HttpPost("generate-scrobble-events")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public ActionResult GenerateScrobbleEvents()
    {
        BackgroundJob.Enqueue(() => scrobblingService.CreateEventsFromExistingHistory(UserId));

        return Ok();
    }

    /// <summary>
    /// Checks if the current Scrobbling token for the given Provider has expired for the current user
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    [HttpGet("token-expired")]
    public async Task<ActionResult<bool>> HasTokenExpired(ScrobbleProvider provider)
    {
        return Ok(await scrobblingService.HasTokenExpired(UserId, provider));
    }

    /// <summary>
    /// Returns all scrobbling errors for the instance
    /// </summary>
    /// <remarks>Requires admin</remarks>
    /// <returns></returns>
    [HttpGet("scrobble-errors")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IEnumerable<ScrobbleErrorDto>>> GetScrobbleErrors()
    {
        return Ok(await unitOfWork.ScrobbleRepository.GetScrobbleErrors());
    }

    /// <summary>
    /// Clears the scrobbling errors table
    /// </summary>
    /// <returns></returns>
    [HttpPost("clear-errors")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> ClearScrobbleErrors()
    {
        await unitOfWork.ScrobbleRepository.ClearScrobbleErrors();
        return Ok();
    }

    /// <summary>
    /// Returns the scrobbling history for the user
    /// </summary>
    /// <remarks>User must have a valid license</remarks>
    /// <returns></returns>
    [HttpPost("scrobble-events")]
    public async Task<ActionResult<PagedList<ScrobbleEventDto>>> GetScrobblingEvents([FromQuery] UserParams pagination, [FromBody] ScrobbleEventFilter filter)
    {
        pagination ??= UserParams.Default;
        var events = await unitOfWork.ScrobbleRepository.GetUserEvents(UserId, filter, pagination);
        Response.AddPaginationHeader(events.CurrentPage, events.PageSize, events.TotalCount, events.TotalPages);

        return Ok(events);
    }

    /// <summary>
    /// Returns all scrobble holds for the current user
    /// </summary>
    /// <returns></returns>
    [HttpGet("holds")]
    public async Task<ActionResult<IEnumerable<ScrobbleHoldDto>>> GetScrobbleHolds()
    {
        return Ok(await unitOfWork.UserRepository.GetHolds(UserId));
    }

    /// <summary>
    /// If there is an active hold on the series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("has-hold")]
    public async Task<ActionResult<bool>> HasHold(int seriesId)
    {
        return Ok(await unitOfWork.UserRepository.HasHoldOnSeries(UserId, seriesId));
    }

    /// <summary>
    /// Does the library the series is in allow scrobbling?
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("library-allows-scrobbling")]
    public async Task<ActionResult<bool>> LibraryAllowsScrobbling(int seriesId)
    {
        return Ok(await unitOfWork.LibraryRepository.GetAllowsScrobblingBySeriesId(seriesId));
    }

    /// <summary>
    /// Adds a hold against the Series for user's scrobbling
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("add-hold")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> AddHold(int seriesId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ScrobbleHolds);
        if (user == null) return Unauthorized();
        if (user.ScrobbleHolds.Any(s => s.SeriesId == seriesId))
            return Ok(await localizationService.TranslateAsync(user.Id, "nothing-to-do"));

        var seriesHold = new ScrobbleHoldBuilder()
            .WithSeriesId(seriesId)
            .Build();
        user.ScrobbleHolds.Add(seriesHold);
        unitOfWork.UserRepository.Update(user);
        try
        {
            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync();

            // When a hold is placed on a series, clear any pre-existing Scrobble Events
            await scrobblingService.ClearEventsForSeries(user.Id, seriesId);
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
            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            // Handle other exceptions or log the error
            logger.LogError(ex, "An error occurred while adding the hold");
            return StatusCode(StatusCodes.Status500InternalServerError,
                await localizationService.TranslateAsync(UserId, "nothing-to-do"));
        }
    }

    /// <summary>
    /// Remove a hold against the Series for user's scrobbling
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpDelete("remove-hold")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> RemoveHold(int seriesId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ScrobbleHolds);
        if (user == null) return Unauthorized();

        user.ScrobbleHolds = user.ScrobbleHolds.Where(h => h.SeriesId != seriesId).ToList();

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();
        return Ok();
    }

    /// <summary>
    /// Has the logged in user ran scrobble generation
    /// </summary>
    /// <returns></returns>
    [HttpGet("has-ran-scrobble-gen")]
    public async Task<ActionResult<bool>> HasRanScrobbleGen()
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        return Ok(user is {HasRunScrobbleEventGeneration: true});
    }

    /// <summary>
    /// Delete the given scrobble events if they belong to that user
    /// </summary>
    /// <param name="eventIds"></param>
    /// <returns></returns>
    [HttpPost("bulk-remove-events")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> BulkRemoveScrobbleEvents(IList<long> eventIds)
    {
        var events = await unitOfWork.ScrobbleRepository.GetUserEvents(UserId, eventIds);
        unitOfWork.ScrobbleRepository.Remove(events);
        await unitOfWork.CommitAsync();
        return Ok();
    }
}
