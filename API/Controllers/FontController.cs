using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Font;
using API.Services;
using API.Services.Tasks;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class FontController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFontService _fontService;
    private readonly ITaskScheduler _taskScheduler;

    public FontController(IUnitOfWork unitOfWork, IFontService fontService, ITaskScheduler taskScheduler)
    {
        _unitOfWork = unitOfWork;
        _fontService = fontService;
        _taskScheduler = taskScheduler;
    }

    [ResponseCache(CacheProfileName = "10Minute")]
    [AllowAnonymous]
    [HttpGet("GetFonts")]
    public async Task<ActionResult<IEnumerable<EpubFontDto>>> GetFonts()
    {
        return Ok(await _unitOfWork.EpubFontRepository.GetFontDtos());
    }

    [AllowAnonymous]
    [HttpGet("download-font")]
    public async Task<IActionResult> GetFont(int fontId)
    {
        try
        {
            var font = await _unitOfWork.EpubFontRepository.GetFont(fontId);
            if (font == null) return NotFound();
            var contentType = GetContentType(font.FileName);
            return File(await _fontService.GetContent(fontId), contentType, font.FileName);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("scan")]
    public IActionResult Scan()
    {
        _taskScheduler.ScanEpubFonts();
        return Ok();
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".ttf" => "application/font-tff",
            ".otf" => "application/font-otf",
            ".woff" => "application/font-woff",
            ".woff2" => "application/font-woff2",
            _ => "application/octet-stream",
        };
    }
}
