using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Dashboard;
using API.DTOs.SideNav;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for anything that deals with Streams (SmartFilters, ExternalSource, DashboardStream, SideNavStream)
/// </summary>
public class StreamController : BaseApiController
{
    private readonly IStreamService _streamService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public StreamController(IStreamService streamService, IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _streamService = streamService;
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Returns the layout of the user's dashboard
    /// </summary>
    /// <returns></returns>
    [HttpGet("dashboard")]
    public async Task<ActionResult<IEnumerable<DashboardStreamDto>>> GetDashboardLayout(bool visibleOnly = true)
    {
        return Ok(await _streamService.GetDashboardStreams(User.GetUserId(), visibleOnly));
    }

    /// <summary>
    /// Return's the user's side nav
    /// </summary>
    [HttpGet("sidenav")]
    public async Task<ActionResult<IEnumerable<SideNavStreamDto>>> GetSideNav(bool visibleOnly = true)
    {
        return Ok(await _streamService.GetSidenavStreams(User.GetUserId(), visibleOnly));
    }

    /// <summary>
    /// Return's the user's external sources
    /// </summary>
    [HttpGet("external-sources")]
    public async Task<ActionResult<IEnumerable<ExternalSourceDto>>> GetExternalSources()
    {
        return Ok(await _streamService.GetExternalSources(User.GetUserId()));
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
        return Ok(await _streamService.CreateExternalSource(User.GetUserId(), dto));
    }

    /// <summary>
    /// Updates an existing external source
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-external-source")]
    public async Task<ActionResult<ExternalSourceDto>> UpdateExternalSource(ExternalSourceDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        // Check if a host and api key exists for the current user
        return Ok(await _streamService.UpdateExternalSource(User.GetUserId(), dto));
    }

    /// <summary>
    /// Validates the external source by host is unique (for this user)
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    [HttpGet("external-source-exists")]
    public async Task<ActionResult<bool>> ExternalSourceExists(string host, string name, string apiKey)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        return Ok(await _unitOfWork.AppUserExternalSourceRepository.ExternalSourceExists(User.GetUserId(), name, host, apiKey));
    }

    /// <summary>
    /// Delete's the external source
    /// </summary>
    /// <param name="externalSourceId"></param>
    /// <returns></returns>
    [HttpDelete("delete-external-source")]
    public async Task<ActionResult> ExternalSourceExists(int externalSourceId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.DeleteExternalSource(User.GetUserId(), externalSourceId);
        return Ok();
    }


    /// <summary>
    /// Creates a Dashboard Stream from a SmartFilter and adds it to the user's dashboard as visible
    /// </summary>
    /// <param name="smartFilterId"></param>
    /// <returns></returns>
    [HttpPost("add-dashboard-stream")]
    public async Task<ActionResult<DashboardStreamDto>> AddDashboard([FromQuery] int smartFilterId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        return Ok(await _streamService.CreateDashboardStreamFromSmartFilter(User.GetUserId(), smartFilterId));
    }

    /// <summary>
    /// Updates the visibility of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-dashboard-stream")]
    public async Task<ActionResult> UpdateDashboardStream(DashboardStreamDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.UpdateDashboardStream(User.GetUserId(), dto);
        return Ok();
    }

    /// <summary>
    /// Updates the position of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-dashboard-position")]
    public async Task<ActionResult> UpdateDashboardStreamPosition(UpdateStreamPositionDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.UpdateDashboardStreamPosition(User.GetUserId(), dto);
        return Ok();
    }


    /// <summary>
    /// Creates a SideNav Stream from a SmartFilter and adds it to the user's sidenav as visible
    /// </summary>
    /// <param name="smartFilterId"></param>
    /// <returns></returns>
    [HttpPost("add-sidenav-stream")]
    public async Task<ActionResult<SideNavStreamDto>> AddSideNav([FromQuery] int smartFilterId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        return Ok(await _streamService.CreateSideNavStreamFromSmartFilter(User.GetUserId(), smartFilterId));
    }

    /// <summary>
    /// Creates a SideNav Stream from a SmartFilter and adds it to the user's sidenav as visible
    /// </summary>
    /// <param name="externalSourceId"></param>
    /// <returns></returns>
    [HttpPost("add-sidenav-stream-from-external-source")]
    public async Task<ActionResult<SideNavStreamDto>> AddSideNavFromExternalSource([FromQuery] int externalSourceId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        return Ok(await _streamService.CreateSideNavStreamFromExternalSource(User.GetUserId(), externalSourceId));
    }

    /// <summary>
    /// Updates the visibility of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-sidenav-stream")]
    public async Task<ActionResult> UpdateSideNavStream(SideNavStreamDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.UpdateSideNavStream(User.GetUserId(), dto);
        return Ok();
    }

    /// <summary>
    /// Updates the position of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-sidenav-position")]
    public async Task<ActionResult> UpdateSideNavStreamPosition(UpdateStreamPositionDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.UpdateSideNavStreamPosition(User.GetUserId(), dto);
        return Ok();
    }

    [HttpPost("bulk-sidenav-stream-visibility")]
    public async Task<ActionResult> BulkUpdateSideNavStream(BulkUpdateSideNavStreamVisibilityDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        await _streamService.UpdateSideNavStreamBulk(User.GetUserId(), dto);
        return Ok();
    }
}
