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
    /// Removes a font family from the system. The family is validated for in-use server side and only removed when
    /// it is not selected by a user, or when an admin forces the deletion.
    /// </summary>
    /// <param name="fontId">Any file within the family to delete</param>
    /// <param name="force">If the family is in use and an admin wants it deleted, they must confirm to force delete it. This is prompted in the UI.</param>
    /// <returns>The result of the deletion. When <see cref="FontDeleteResultDto.InUse"/> is true and it was not deleted, the UI prompts an admin to force.</returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<FontDeleteResultDto>> DeleteFont(int fontId, bool force = false)
    {
        var forceDelete = User.IsInRole(PolicyConstants.AdminRole) && force;
        return Ok(await fontService.DeleteFamily(fontId, forceDelete));
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
    public async Task<ActionResult<EpubFontDto[]>> UploadFontByUrl([FromQuery] string url)
    {
        try
        {
            var fonts = await fontService.CreateFontsFromUrl(url);
            return Ok(mapper.Map<EpubFontDto[]>(fonts));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }
    }

}
