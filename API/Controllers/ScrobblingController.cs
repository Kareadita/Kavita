using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Account;
using API.DTOs.KavitaPlus.Account;
using API.DTOs.Scrobbling;
using API.Entities.Scrobble;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class ScrobblingController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILogger<ScrobblingController> _logger;
    private readonly ILocalizationService _localizationService;

    public ScrobblingController(IUnitOfWork unitOfWork, IScrobblingService scrobblingService,
        ILogger<ScrobblingController> logger, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _scrobblingService = scrobblingService;
        _logger = logger;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Get the current user's AniList token
    /// </summary>
    /// <returns></returns>
    [HttpGet("anilist-token")]
    public async Task<ActionResult<string>> GetAniListToken()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        return Ok(user.AniListAccessToken);
    }

    /// <summary>
    /// Get the current user's MAL token & username
    /// </summary>
    /// <returns></returns>
    [HttpGet("mal-token")]
    public async Task<ActionResult<MalUserInfoDto>> GetMalToken()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
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
    public async Task<ActionResult<bool>> UpdateAniListToken(AniListUpdateDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        var isNewToken = string.IsNullOrEmpty(user.AniListAccessToken);
        user.AniListAccessToken = dto.Token;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        return Ok(isNewToken);
    }

    /// <summary>
    /// Update the current user's MAL token (Client ID) and Username
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>True if the token was new or not</returns>
    [HttpPost("update-mal-token")]
    public async Task<ActionResult<bool>> UpdateMalToken(MalUserInfoDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        var isNewToken = string.IsNullOrEmpty(user.MalAccessToken);
        user.MalAccessToken = dto.AccessToken;
        user.MalUserName = dto.Username;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        return Ok(isNewToken);
    }

    /// <summary>
    /// When a user request to generate scrobble events from history. Should only be ran once per user.
    /// </summary>
    /// <returns></returns>
    [HttpPost("generate-scrobble-events")]
    public ActionResult GenerateScrobbleEvents()
    {
        BackgroundJob.Enqueue(() => _scrobblingService.CreateEventsFromExistingHistory(User.GetUserId()));

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
    [HttpPost("scrobble-events")]
    public async Task<ActionResult<PagedList<ScrobbleEventDto>>> GetScrobblingEvents([FromQuery] UserParams pagination, [FromBody] ScrobbleEventFilter filter)
    {
        pagination ??= UserParams.Default;
        var events = await _unitOfWork.ScrobbleRepository.GetUserEvents(User.GetUserId(), filter, pagination);
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
        return Ok(await _unitOfWork.LibraryRepository.GetAllowsScrobblingBySeriesId(seriesId));
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
        if (user.ScrobbleHolds.Any(s => s.SeriesId == seriesId))
            return Ok(await _localizationService.Translate(user.Id, "nothing-to-do"));

        var seriesHold = new ScrobbleHoldBuilder()
            .WithSeriesId(seriesId)
            .Build();
        user.ScrobbleHolds.Add(seriesHold);
        _unitOfWork.UserRepository.Update(user);
        try
        {
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();

            // When a hold is placed on a series, clear any pre-existing Scrobble Events
            await _scrobblingService.ClearEventsForSeries(user.Id, seriesId);
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
            _logger.LogError(ex, "An error occurred while adding the hold");
            return StatusCode(StatusCodes.Status500InternalServerError,
                await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
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
