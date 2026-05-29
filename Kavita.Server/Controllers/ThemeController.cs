using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Theme;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

public class ThemeController(
    IUnitOfWork unitOfWork,
    IThemeService themeService,
    ILocalizationService localizationService,
    IDirectoryService directoryService,
    IMapper mapper)
    : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SiteThemeDto>>> GetThemes()
    {
        return Ok(await unitOfWork.SiteThemeRepository.GetThemeDtos());
    }


    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("update-default")]
    public async Task<ActionResult> UpdateDefault(UpdateDefaultThemeDto dto)
    {
        try
        {
            await themeService.UpdateDefault(dto.ThemeId);
        }
        catch (KavitaException)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "theme-doesnt-exist"));
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
            return Ok(await themeService.GetContent(themeId));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.GetAsync("en", ex.Message));
        }
    }

    /// <summary>
    /// Browse themes that can be used on this server
    /// </summary>
    /// <returns></returns>
    [HttpGet("browse")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute)]
    public async Task<ActionResult<IEnumerable<DownloadableSiteThemeDto>>> BrowseThemes()
    {
        var themes = await themeService.GetDownloadableThemes();
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
        await themeService.DeleteTheme(themeId);

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
        return Ok(mapper.Map<SiteThemeDto>(await themeService.DownloadRepoTheme(dto)));
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
        if (!IsPathWithinDirectory(directoryService.TempDirectory, formFile.FileName)) return BadRequest("Invalid file");
        var tempFile = await UploadToTempAsync(formFile);

        // Set summary as "Uploaded by Username! on DATE"
        var theme = await themeService.CreateThemeFromFile(tempFile, Username!);
        return Ok(mapper.Map<SiteThemeDto>(theme));
    }

}
