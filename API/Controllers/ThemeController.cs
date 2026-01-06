using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Theme;
using API.Middleware;
using API.Services;
using API.Services.Tasks;
using AutoMapper;
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
    private readonly ILocalizationService _localizationService;
    private readonly IDirectoryService _directoryService;
    private readonly IMapper _mapper;


    public ThemeController(IUnitOfWork unitOfWork, IThemeService themeService,
        ILocalizationService localizationService, IDirectoryService directoryService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _themeService = themeService;
        _localizationService = localizationService;
        _directoryService = directoryService;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SiteThemeDto>>> GetThemes()
    {
        return Ok(await _unitOfWork.SiteThemeRepository.GetThemeDtos());
    }


    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("update-default")]
    public async Task<ActionResult> UpdateDefault(UpdateDefaultThemeDto dto)
    {
        try
        {
            await _themeService.UpdateDefault(dto.ThemeId);
        }
        catch (KavitaException)
        {
            return BadRequest(await _localizationService.Translate(UserId, "theme-doesnt-exist"));
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour)]
    public async Task<ActionResult<IEnumerable<DownloadableSiteThemeDto>>> BrowseThemes()
    {
        var themes = await _themeService.GetDownloadableThemes();
        return Ok(themes.Where(t => !t.AlreadyDownloaded));
    }

    /// <summary>
    /// Attempts to delete a theme. If already in use by users, will not allow
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<IEnumerable<DownloadableSiteThemeDto>>> DeleteTheme(int themeId)
    {
        await _themeService.DeleteTheme(themeId);

        return Ok();
    }

    /// <summary>
    /// Downloads a SiteTheme from upstream
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("download-theme")]
    public async Task<ActionResult<SiteThemeDto>> DownloadTheme(DownloadableSiteThemeDto dto)
    {
        return Ok(_mapper.Map<SiteThemeDto>(await _themeService.DownloadRepoTheme(dto)));
    }

    /// <summary>
    /// Uploads a new theme file
    /// </summary>
    /// <param name="formFile"></param>
    /// <returns></returns>
    [HttpPost("upload-theme")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<SiteThemeDto>> DownloadTheme(IFormFile formFile)
    {
        if (!formFile.FileName.EndsWith(".css")) return BadRequest("Invalid file");
        if (formFile.FileName.Contains("..")) return BadRequest("Invalid file");
        var tempFile = await UploadToTemp(formFile);

        // Set summary as "Uploaded by Username! on DATE"
        var theme = await _themeService.CreateThemeFromFile(tempFile, Username!);
        return Ok(_mapper.Map<SiteThemeDto>(theme));
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
