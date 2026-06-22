using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Scrobble;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskScheduler = Kavita.Services.TaskScheduler;

namespace Kavita.Server.Controllers;

public class ScrobblingController(
    IUnitOfWork unitOfWork,
    IScrobblingService scrobblingService,
    IScrobbleRuleService ruleService,
    ILogger<ScrobblingController> logger,
    ILocalizationService localizationService,
    IKavitaPlusAuditService kavitaPlusAuditService,
    IMapper mapper)
    : BaseApiController
{

    /// <summary>
    /// Returns all scrobble providers for a user. This list is guaranteed to contain an entry for each currently
    /// valid scrobble provider. If the user has none setup, returns the empty default values.
    /// </summary>
    /// <returns></returns>
    [HttpGet("scrobble-settings")]
    public async Task<ActionResult<List<ScrobbleProviderDto>>> GetScrobbleSettings()
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.UserPreferences);
        if (user == null) return Unauthorized();

        var providers = user.ScrobbleProviders.Values
            .Select(mapper.Map<ScrobbleProviderDto>)
            .ToList();

        return Ok(providers);
    }

    /// <summary>
    /// Updates the scrobble settings for a given provider. Libraries are filtered on supported types
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="scrobbleSettings"></param>
    /// <returns></returns>
    [HttpPost("update-scrobble-settings")]
    public async Task<ActionResult> UpdateScrobbleSettings([FromQuery] ScrobbleProvider provider, [FromBody] ScrobbleProviderSettingsDto scrobbleSettings)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.UserPreferences);
        if (user == null) return Unauthorized();

        var scrobbleProvider = user.ScrobbleProviders[provider];
        scrobbleProvider.Settings = scrobbleSettings;

        if (scrobbleProvider.Settings.AllLibraries)
        {
            scrobbleProvider.Settings.Libraries = [];
        }
        else if (scrobbleProvider.Settings.Libraries.Count > 0)
        {
            scrobbleProvider.Settings.Libraries = await scrobblingService
                .FilterLibrariesForProvider(provider, UserId, scrobbleProvider.Settings.Libraries);
        }

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        // We don't want this on a background thread to ensure clearance from quick updates
        await ruleService.PurgeStaleForSettingsAsync(UserId, provider, scrobbleSettings);

        return Ok();
    }

    /// <summary>
    /// Update authentication details for the given provider
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <remarks>Kicks of a sync background job, listen on signalr for when it completes</remarks>
    [HttpPost("update-user-scrobble-provider")]
    public async Task<ActionResult> UpdateUserScrobbleProvider([FromBody] UpdateScrobbleProviderDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, ct: HttpContext.RequestAborted);
        if (user == null) return Unauthorized();

        var scrobbleProvider = user.ScrobbleProviders[dto.Provider];

        scrobbleProvider.AuthenticationToken = dto.AuthenticationToken.TrimPrefix("Bearer").Trim();
        scrobbleProvider.RefreshToken = dto.RefreshToken.TrimPrefix("Bearer").Trim();

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(dto.AuthenticationToken))
        {
            await unitOfWork.ScrobbleRepository.ClearEventsForProvider(UserId, dto.Provider);
            await ruleService.PurgeForProviderAsync(UserId, dto.Provider);
        }

        BackgroundJob.Enqueue(() => scrobblingService.SyncProviderInfo(UserId, dto.Provider, CancellationToken.None));

        return Ok();
    }

    /// <summary>
    /// Generate scrobble events from history. Should only be ran once per user.
    /// </summary>
    /// <returns></returns>
    [HttpPost("generate-scrobble-events")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> GenerateScrobbleEvents([FromQuery] ScrobbleProvider scrobbleProvider)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        if (user == null) return Unauthorized();

        BackgroundJob.Enqueue(() => scrobblingService.CreateEventsFromExistingHistory(scrobbleProvider, UserId));

        return Ok();
    }

    /// <summary>
    /// Generate scrobble events from history for all valid providers.
    /// </summary>
    /// <returns></returns>
    [HttpPost("generate-scrobble-events-all")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> GenerateScrobbleEventsAll()
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        if (user == null) return Unauthorized();

        var providers = user.ScrobbleProviders
            .Where(kv => !string.IsNullOrEmpty(kv.Value.AuthenticationToken) && kv.Value.ValidUntilUtc > DateTime.UtcNow)
            .Select(kv => kv.Key)
            .ToList();

        if (providers.Count > 0)
        {
            BackgroundJob.Enqueue(() => scrobblingService.CreateEventsFromExistingHistory(providers, UserId, CancellationToken.None));
        }

        return Ok(providers.Count > 0);
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
    /// Returns all expired tokens for the current user
    /// </summary>
    /// <returns></returns>
    [HttpGet("expired-tokens")]
    public async Task<ActionResult<List<ScrobbleProvider>>> GetExpiredTokens()
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        if (user == null) return Unauthorized();

        // MAL doesn't have a validUntil, thus the date will be 1/1/0001. Just filter that out so it doesn't always proc
        return Ok(user.ScrobbleProviders
            .Where(kv => kv.Value.ValidUntilUtc.Year != 1 && kv.Value.ValidUntilUtc < DateTime.UtcNow && !string.IsNullOrEmpty(kv.Value.AuthenticationToken))
            .Select(kv => kv.Key)
            .ToList()
        );
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
            await kavitaPlusAuditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleHoldAdded, seriesId,
                new AuditLogScrobbleParamsDto(), AuditStatus.Success, null, UserId, HttpContext.RequestAborted);
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
            await kavitaPlusAuditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleHoldAdded, seriesId,
                new AuditLogScrobbleParamsDto(), AuditStatus.Success, null, UserId, HttpContext.RequestAborted);
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
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ScrobbleHolds, HttpContext.RequestAborted);
        if (user == null) return Unauthorized();

        user.ScrobbleHolds = user.ScrobbleHolds.Where(h => h.SeriesId != seriesId).ToList();

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(HttpContext.RequestAborted);

        await kavitaPlusAuditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleHoldRemoved, seriesId,
            new AuditLogScrobbleParamsDto(), AuditStatus.Success, null, UserId, HttpContext.RequestAborted);

        return Ok();
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


    /// <summary>
    /// Attempts to retry Scrobble Events for the current authenticated user (or admin-allowed).
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>true if successful, false in all other cases (validation)</returns>
    [HttpPost("retry-scrobble")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> RetryScrobble(KavitaPlusAuditEntryDto dto)
    {
        if (!dto.UserId.HasValue) return Ok(false);
        if (dto.UserId != UserId && !User.IsInRole(PolicyConstants.AdminRole)) return Ok(false);

        // Locate the Scrobble event or replay the event
        return Ok(await scrobblingService.RetryScrobbleAsync(UserId, dto, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns when Scrobbling upload will next execute
    /// </summary>
    /// <returns></returns>
    [HttpGet("next-scrobble-time")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult<DateTime?> GetNextScrobbleTime()
    {
        return Ok(TaskScheduler.GetNextRun(TaskSchedulerConstants.ProcessScrobblingEventsId));
    }
}
