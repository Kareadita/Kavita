using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.SideNav;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;
/// <summary>
/// Responsible for anything that deals with Streams (SmartFilters, ExternalSource, DashboardStream, SideNavStream)
/// </summary>
public class StreamController(
    IStreamService streamService,
    IUnitOfWork unitOfWork)
    : BaseApiController
{
    /// <summary>
    /// Returns the layout of the user's dashboard
    /// </summary>
    /// <returns></returns>
    [HttpGet("dashboard")]
    public async Task<ActionResult<IEnumerable<DashboardStreamDto>>> GetDashboardLayout(bool visibleOnly = true)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.GetDashboardStreams(UserId, visibleOnly, ct));
    }

    /// <summary>
    /// Return's the user's side nav
    /// </summary>
    [HttpGet("sidenav")]
    public async Task<ActionResult<IEnumerable<SideNavStreamDto>>> GetSideNav(bool visibleOnly = true)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.GetSidenavStreams(UserId, visibleOnly, ct));
    }

    /// <summary>
    /// Return's the user's external sources
    /// </summary>
    [HttpGet("external-sources")]
    public async Task<ActionResult<IEnumerable<ExternalSourceDto>>> GetExternalSources()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.GetExternalSources(UserId, ct));
    }

    /// <summary>
    /// Create an external Source
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create-external-source")]
    public async Task<ActionResult<ExternalSourceDto>> CreateExternalSource(ExternalSourceDto dto)
    {
        // Check if a host and api key exists for the current user
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.CreateExternalSource(UserId, dto, ct));
    }

    /// <summary>
    /// Updates an existing external source
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-external-source")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<ExternalSourceDto>> UpdateExternalSource(ExternalSourceDto dto)
    {
        // Check if a host and api key exists for the current user
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.UpdateExternalSource(UserId, dto, ct));
    }

    /// <summary>
    /// Validates the external source by host is unique (for this user)
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("external-source-exists")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> ExternalSourceExists(ExternalSourceDto dto)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.AppUserExternalSourceRepository.ExternalSourceExists(UserId, dto.Name, dto.Host, dto.ApiKey, ct));
    }

    /// <summary>
    /// Delete's the external source
    /// </summary>
    /// <param name="externalSourceId"></param>
    /// <returns></returns>
    [HttpDelete("delete-external-source")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> ExternalSourceExists(int externalSourceId)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.DeleteExternalSource(UserId, externalSourceId, ct);
        return Ok();
    }


    /// <summary>
    /// Creates a Dashboard Stream from a SmartFilter and adds it to the user's dashboard as visible
    /// </summary>
    /// <param name="smartFilterId"></param>
    /// <returns></returns>
    [HttpPost("add-dashboard-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<DashboardStreamDto>> AddDashboard([FromQuery] int smartFilterId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.CreateDashboardStreamFromSmartFilter(UserId, smartFilterId, ct));
    }

    /// <summary>
    /// Updates the visibility of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-dashboard-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateDashboardStream(DashboardStreamDto dto)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.UpdateDashboardStream(UserId, dto, ct);
        return Ok();
    }

    /// <summary>
    /// Updates the position of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-dashboard-position")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateDashboardStreamPosition(UpdateStreamPositionDto dto)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.UpdateDashboardStreamPosition(UserId, dto, ct);
        return Ok();
    }


    /// <summary>
    /// Creates a SideNav Stream from a SmartFilter and adds it to the user's sidenav as visible
    /// </summary>
    /// <param name="smartFilterId"></param>
    /// <returns></returns>
    [HttpPost("add-sidenav-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<SideNavStreamDto>> AddSideNav([FromQuery] int smartFilterId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.CreateSideNavStreamFromSmartFilter(UserId, smartFilterId, ct));
    }

    /// <summary>
    /// Creates a SideNav Stream from a SmartFilter and adds it to the user's sidenav as visible
    /// </summary>
    /// <param name="externalSourceId"></param>
    /// <returns></returns>
    [HttpPost("add-sidenav-stream-from-external-source")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<SideNavStreamDto>> AddSideNavFromExternalSource([FromQuery] int externalSourceId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await streamService.CreateSideNavStreamFromExternalSource(UserId, externalSourceId, ct));
    }

    /// <summary>
    /// Updates the visibility of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-sidenav-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateSideNavStream(SideNavStreamDto dto)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.UpdateSideNavStream(UserId, dto, ct);
        return Ok();
    }

    /// <summary>
    /// Updates the position of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-sidenav-position")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateSideNavStreamPosition(UpdateStreamPositionDto dto)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.UpdateSideNavStreamPosition(UserId, dto, ct);
        return Ok();
    }

    [HttpPost("bulk-sidenav-stream-visibility")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> BulkUpdateSideNavStream(BulkUpdateSideNavStreamVisibilityDto dto)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.UpdateSideNavStreamBulk(UserId, dto, ct);
        return Ok();
    }

    /// <summary>
    /// Removes a Smart Filter from a user's SideNav Streams
    /// </summary>
    /// <param name="sideNavStreamId"></param>
    /// <returns></returns>
    [HttpDelete("smart-filter-side-nav-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteSmartFilterSideNavStream([FromQuery] int sideNavStreamId)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.DeleteSideNavSmartFilterStream(UserId, sideNavStreamId, ct);
        return Ok();
    }

    /// <summary>
    /// Removes a Smart Filter from a user's Dashboard Streams
    /// </summary>
    /// <param name="dashboardStreamId"></param>
    /// <returns></returns>
    [HttpDelete("smart-filter-dashboard-stream")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteSmartFilterDashboardStream([FromQuery] int dashboardStreamId)
    {
        var ct = HttpContext.RequestAborted;
        await streamService.DeleteDashboardSmartFilterStream(UserId, dashboardStreamId, ct);
        return Ok();
    }
}
