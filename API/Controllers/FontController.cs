using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Font;
using API.Entities.Enums.Font;
using API.Middleware;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

[Authorize]
public class FontController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IFontService _fontService;
    private readonly IMapper _mapper;
    private readonly ILocalizationService _localizationService;

    private readonly Regex _fontFileExtensionRegex = new(Parser.FontFileExtensions, RegexOptions.IgnoreCase, Parser.RegexTimeout);

    public FontController(IUnitOfWork unitOfWork, IDirectoryService directoryService,
        IFontService fontService, IMapper mapper, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _fontService = fontService;
        _mapper = mapper;
        _localizationService = localizationService;
    }

    /// <summary>
    /// List out the fonts
    /// </summary>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.TenMinute)]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<EpubFontDto>>> GetFonts()
    {
        return Ok(await _unitOfWork.EpubFontRepository.GetFontDtosAsync());
    }

    /// <summary>
    /// Returns a font file
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFont(int fontId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();

        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null) return NotFound();

        if (font.Provider == FontProvider.System) return BadRequest("System provided fonts are not loaded by API");


        var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(font.FileName));
        var path = Path.Join(_directoryService.EpubFontDirectory, font.FileName);

        return PhysicalFile(path, contentType, true);
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
        var fontInUse = await _fontService.IsFontInUse(fontId);
        if (!fontInUse || forceDelete)
        {
            await _fontService.Delete(fontId);
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
        return Ok(await _fontService.IsFontInUse(fontId));
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

        if (formFile.FileName.Contains("..")) return BadRequest("Invalid file");


        var tempFile = await UploadToTemp(formFile);
        var font = await _fontService.CreateFontFromFileAsync(tempFile);
        return Ok(_mapper.Map<EpubFontDto>(font));
    }

    [HttpPost("upload-by-url")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UploadFontByUrl([FromQuery]string url)
    {
        // Validate url
        try
        {
            var font = await _fontService.CreateFontFromUrl(url);
            return Ok(_mapper.Map<EpubFontDto>(font));
        }
        catch (KavitaException ex)
        {
            return BadRequest(_localizationService.Translate(UserId, ex.Message));
        }
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
