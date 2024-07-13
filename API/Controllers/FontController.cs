using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Font;
using API.Entities.Enums.Font;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

public class FontController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IFontService _fontService;
    private readonly IMapper _mapper;

    private readonly Regex _fontFileExtensionRegex = new(Parser.FontFileExtensions, RegexOptions.IgnoreCase, Parser.RegexTimeout);

    public FontController(IUnitOfWork unitOfWork, IDirectoryService directoryService,
        IFontService fontService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _fontService = fontService;
        _mapper = mapper;
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
    /// Returns a font
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFont(int fontId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return BadRequest();

        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null) return NotFound();

        // var fontDirectory = _directoryService.EpubFontDirectory;
        // if (font.Provider == FontProvider.System)
        // {
        //     fontDirectory = _directoryService.
        // }

        var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(font.FileName));
        var path = Path.Join(_directoryService.EpubFontDirectory, font.FileName);

        return PhysicalFile(path, contentType);
    }

    /// <summary>
    /// Removes a font from the system
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="confirmed">If the font is in use by other users and an admin wants it deleted, they must confirm to force delete it</param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<IActionResult> DeleteFont(int fontId, bool confirmed = false)
    {
        // TODO: We need to check if this font is used by anyone else and if so, need to inform the user
        // Need to check if this is a system font as well
        var forceDelete = User.IsInRole(PolicyConstants.AdminRole) && confirmed;
        await _fontService.Delete(fontId);
        return Ok();
    }

    /// <summary>
    /// Manual upload
    /// </summary>
    /// <param name="formFile"></param>
    /// <returns></returns>
    [HttpPost("upload")]
    public async Task<ActionResult<EpubFontDto>> UploadFont(IFormFile formFile)
    {
        if (!_fontFileExtensionRegex.IsMatch(Path.GetExtension(formFile.FileName))) return BadRequest("Invalid file");

        if (formFile.FileName.Contains("..")) return BadRequest("Invalid file");


        var tempFile = await UploadToTemp(formFile);
        var font = await _fontService.CreateFontFromFileAsync(tempFile);
        return Ok(_mapper.Map<EpubFontDto>(font));
    }

    // [HttpPost("upload-url")]
    // public async Task<ActionResult<EpubFontDto>> UploadFontByUrl(string url)
    // {
    //     throw new NotImplementedException();
    // }

    private async Task<string> UploadToTemp(IFormFile file)
    {
        var outputFile = Path.Join(_directoryService.TempDirectory, file.FileName);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return outputFile;
    }
}
