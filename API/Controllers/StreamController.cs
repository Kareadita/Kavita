using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Dashboard;
using API.DTOs.SideNav;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class StreamController : BaseApiController
{
    private readonly IStreamService _streamService;
    private readonly ILogger<StreamController> _logger;

    public StreamController(IStreamService streamService, ILogger<StreamController> logger)
    {
        _streamService = streamService;
        _logger = logger;
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
    /// Return's the user's side nav
    /// </summary>
    [HttpGet("external-sources")]
    public async Task<ActionResult<IEnumerable<SideNavStreamDto>>> GetExternalSources()
    {
        return Ok(await _streamService.GetExternalSources(User.GetUserId()));
    }


    /// <summary>
    /// Creates a Dashboard Stream from a SmartFilter and adds it to the user's dashboard as visible
    /// </summary>
    /// <param name="smartFilterId"></param>
    /// <returns></returns>
    [HttpPost("add-dashboard-stream")]
    public async Task<ActionResult<DashboardStreamDto>> AddDashboard([FromQuery] int smartFilterId)
    {
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
        return Ok(await _streamService.CreateSideNavStreamFromSmartFilter(User.GetUserId(), smartFilterId));
    }

    /// <summary>
    /// Updates the visibility of a dashboard stream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-sidenav-stream")]
    public async Task<ActionResult> UpdateSideNavStream(SideNavStreamDto dto)
    {
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
        await _streamService.UpdateSideNavStreamPosition(User.GetUserId(), dto);
        return Ok();
    }
}
