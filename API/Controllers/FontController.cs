using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Font;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeTypes;
using Serilog;

namespace API.Controllers;

public class FontController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IFontService _fontService;
    private readonly IMapper _mapper;

    private readonly Regex _fontFileExtensionRegex = new(Parser.FontFileExtensions, RegexOptions.IgnoreCase, Parser.RegexTimeout);

    public FontController(IUnitOfWork unitOfWork, ITaskScheduler taskScheduler, IDirectoryService directoryService,
        IFontService fontService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _taskScheduler = taskScheduler;
        _fontService = fontService;
        _mapper = mapper;
    }

    [ResponseCache(CacheProfileName = "10Minute")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<EpubFontDto>>> GetFonts()
    {
        return Ok(await _unitOfWork.EpubFontRepository.GetFontDtosAsync());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFont(int fontId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);

        if (userId == 0) return BadRequest();

        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);

        if (font == null) return NotFound();

        var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(font.FileName));
        var path = Path.Join(_directoryService.EpubFontDirectory, font.FileName);
        return PhysicalFile(path, contentType);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteFont(int fontId)
    {
        await _fontService.Delete(fontId);
        return Ok();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<EpubFontDto>> UploadFont(IFormFile formFile)
    {
        if (!_fontFileExtensionRegex.IsMatch(Path.GetExtension(formFile.FileName)))
            return BadRequest("Invalid file");

        if (formFile.FileName.Contains(".."))
            return BadRequest("Invalid file");

        var tempFile = await UploadToTemp(formFile);
        var font = await _fontService.CreateFontFromFileAsync(tempFile);
        return Ok(_mapper.Map<EpubFontDto>(font));
    }

    [HttpPost("upload-url")]
    public async Task<ActionResult<EpubFontDto>> UploadFontByUrl(string url)
    {
        throw new NotImplementedException();
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
