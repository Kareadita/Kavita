using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.ReadingLists.CBL;
using API.DTOs.Theme;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

public class ThemeController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IThemeService _themeService;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILocalizationService _localizationService;
    private readonly IDirectoryService _directoryService;

    public ThemeController(IUnitOfWork unitOfWork, IThemeService themeService, ITaskScheduler taskScheduler,
        ILocalizationService localizationService, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _themeService = themeService;
        _taskScheduler = taskScheduler;
        _localizationService = localizationService;
        _directoryService = directoryService;
    }

    [ResponseCache(CacheProfileName = "10Minute")]
    [AllowAnonymous]
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

    [Authorize("RequireAdminRole")]
    [HttpPost("update-default")]
    public async Task<ActionResult> UpdateDefault(UpdateDefaultThemeDto dto)
    {
        try
        {
            await _themeService.UpdateDefault(dto.ThemeId);
        }
        catch (KavitaException)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "theme-doesnt-exist"));
        }

        return Ok();
    }

    /// <summary>
    /// Returns css content to the UI. UI is expected to escape the content
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("download-content")]
    public async Task<ActionResult<string>> GetThemeContent(int themeId)
    {
        try
        {
            return Ok(await _themeService.GetContent(themeId));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Get("en", ex.Message));
        }
    }

    /// <summary>
    /// Browse themes that can be used on this server
    /// </summary>
    /// <returns></returns>
    [HttpGet("browse")]
    public async Task<ActionResult<IEnumerable<DownloadableSiteThemeDto>>> BrowseThemes()
    {
        // await _unitOfWork.SiteThemeRepository.GetThemeDtos() to show which ones
        return Ok(await _themeService.BrowseRepoThemes());
    }

    /// <summary>
    /// Downloads a SiteTheme from upstream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("download-theme")]
    public async Task<ActionResult> DownloadTheme(DownloadableSiteThemeDto dto)
    {
        await _themeService.DownloadRepoTheme(dto);
        return Ok();
    }

    [HttpPost("upload-theme")]
    public async Task<ActionResult> DownloadTheme(IFormFile formFile)
    {
        var tempFile = await UploadToTemp(formFile);



        return Ok();
    }

    private async Task<string> UploadToTemp(IFormFile file)
    {
        var outputFile = Path.Join(_directoryService.TempDirectory, file.FileName);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return outputFile;
    }

}
