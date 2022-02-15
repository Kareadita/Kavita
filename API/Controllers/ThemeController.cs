using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Theme;
using API.Services;
using API.Services.Tasks;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class ThemeController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISiteThemeService _siteThemeService;
    private readonly ITaskScheduler _taskScheduler;

    public ThemeController(IUnitOfWork unitOfWork, ISiteThemeService siteThemeService, ITaskScheduler taskScheduler)
    {
        _unitOfWork = unitOfWork;
        _siteThemeService = siteThemeService;
        _taskScheduler = taskScheduler;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SiteThemeDto>>> GetThemes()
    {
        return Ok(await _unitOfWork.SiteThemeRepository.GetThemeDtos());
    }

    [Authorize("RequireAdminRole")]
    [HttpPost("scan")]
    public ActionResult Scan()
    {
        _taskScheduler.ScanSiteThemes();
        return Ok();
    }

    /// <summary>
    /// Returns css content to the UI. UI is expected to escape the content
    /// </summary>
    /// <returns></returns>
    [HttpGet("download-content")]
    public async Task<ActionResult<string>> GetThemeContent(int themeId)
    {
        try
        {
            return Ok(await _siteThemeService.GetContent(themeId));
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
