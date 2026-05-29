using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Font;
using Kavita.Models.Entities.Enums.Font;
using Kavita.Server.Attributes;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[Authorize]
public class FontController(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IFontService fontService,
    IMapper mapper,
    ILocalizationService localizationService)
    : BaseApiController
{
    private readonly Regex _fontFileExtensionRegex = new(Parser.FontFileExtensions, RegexOptions.IgnoreCase, Parser.RegexTimeout);

    /// <summary>
    /// List out the fonts
    /// </summary>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<EpubFontDto>>> GetFonts()
    {
        return Ok(await unitOfWork.EpubFontRepository.GetFontDtosAsync());
    }

    /// <summary>
    /// Returns a font file
    /// </summary>
    /// <param name="fontId"></param>
    /// <returns></returns>
    [HttpGet]
    [SkipDeviceTracking]
    public async Task<IActionResult> GetFont(int fontId)
    {
        var font = await unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null) return NotFound();

        if (font.Provider == FontProvider.System) return BadRequest("System provided fonts are not loaded by API");

        if (!IsPathWithinDirectory(directoryService.EpubFontDirectory, font.FileName)) return NotFound();

        var path = Path.Join(directoryService.EpubFontDirectory, font.FileName);

        return CachedFile(path);
    }

    /// <summary>
    /// Removes a font from the system
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="force">If the font is in use by other users and an admin wants it deleted, they must confirm to force delete it. This is prompted in the UI.</param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<IActionResult> DeleteFont(int fontId, bool force = false)
    {
        var forceDelete = User.IsInRole(PolicyConstants.AdminRole) && force;
        var fontInUse = await fontService.IsFontInUse(fontId);
        if (!fontInUse || forceDelete)
        {
            await fontService.Delete(fontId);
        }

        return Ok();
    }

    /// <summary>
    /// Returns if the given font is in use by any other user. System provided fonts will always return true.
    /// </summary>
    /// <param name="fontId"></param>
    /// <returns></returns>
    [HttpGet("in-use")]
    public async Task<ActionResult<bool>> IsFontInUse(int fontId)
    {
        return Ok(await fontService.IsFontInUse(fontId));
    }

    /// <summary>
    /// Manual upload
    /// </summary>
    /// <param name="formFile"></param>
    /// <returns></returns>
    [HttpPost("upload")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<EpubFontDto>> UploadFont(IFormFile formFile)
    {
        if (!_fontFileExtensionRegex.IsMatch(Path.GetExtension(formFile.FileName))) return BadRequest("Invalid file");

        if (!IsPathWithinDirectory(directoryService.TempDirectory, formFile.FileName)) return BadRequest("Invalid file");


        var tempFile = await UploadToTempAsync(formFile);
        var font = await fontService.CreateFontFromFileAsync(tempFile);
        return Ok(mapper.Map<EpubFontDto>(font));
    }

    [HttpPost("upload-by-url")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UploadFontByUrl([FromQuery]string url)
    {
        // Validate url
        try
        {
            var font = await fontService.CreateFontFromUrl(url);
            return Ok(mapper.Map<EpubFontDto>(font));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }
    }

}
