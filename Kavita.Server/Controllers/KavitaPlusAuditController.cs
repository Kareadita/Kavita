using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[KPlus]
[Route("api/kavita-plus-audit")]
public class KavitaPlusAuditController(IUnitOfWork unitOfWork) : BaseApiController
{
    /// <summary>
    /// Returns a paged, filtered list of all Kavita+ audit events. Admin only.
    /// </summary>
    [HttpPost("entries")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<PagedList<KavitaPlusAuditEntryDto>>> GetEntries(
        KavitaPlusAuditFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var res = await unitOfWork.KavitaPlusAuditRepository.GetPagedAsync(filter, userParams);
        Response.AddPaginationHeader(res);

        return Ok(res);
    }

    /// <summary>
    /// Returns Kavita+ audit info scoped to a single series, for the popover.
    /// Scrobble events are filtered to the calling user unless they are an admin.
    /// </summary>
    [HttpGet("entries/series/{seriesId:int}")]
    [SeriesAccess]
    public async Task<ActionResult<KavitaPlusAuditSeriesInfoDto>> GetSeriesInfo(int seriesId)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        var result = await unitOfWork.KavitaPlusAuditRepository
            .GetSeriesInfoAsync(seriesId, UserId, isAdmin);
        return Ok(result);
    }

    /// <summary>
    /// Returns aggregate stats for the admin audit feed
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<KavitaPlusAuditStatsDto>> GetStats()
    {
        return Ok(await unitOfWork.KavitaPlusAuditRepository.GetStatsAsync(HttpContext.RequestAborted));
    }

    /// <summary>
    /// Aggregate stats for my activity audit feed
    /// </summary>
    /// <returns></returns>
    [HttpGet("my-stats")]
    public async Task<ActionResult<KavitaPlusMyAuditStatsDto>> GetMyStats()
    {
        return Ok(await unitOfWork.KavitaPlusAuditRepository.GetMyStatsAsync(UserId, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns the calling user's own Kavita+ activity, paged and filtered.
    /// </summary>
    [HttpPost("my-activity")]
    public async Task<ActionResult<PagedList<KavitaPlusAuditEntryDto>>> GetMyActivity(
        KavitaPlusAuditFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var res = await unitOfWork.KavitaPlusAuditRepository
            .GetMyActivityAsync(UserId, filter, userParams);
        Response.AddPaginationHeader(res);

        return Ok(res);
    }

    /// <summary>
    /// Returns the number of scrobble events that have failed to be sent to Kavita+. (I.e. Has a linked failure audit
    /// & is not sent)
    /// </summary>
    /// <returns></returns>
    [HttpGet("failed-scrobble-events")]
    public async Task<ActionResult<int>> GetCurrentlyFailedScrobbleEvents()
    {
        return Ok(await unitOfWork.KavitaPlusAuditRepository.GetScrobbleFailureCountAsync(UserId, HttpContext.RequestAborted));
    }
}
