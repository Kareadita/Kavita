using System.Text.Encodings.Web;
using API.Data;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class ThemeController : BaseApiController
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HtmlEncoder _htmlEncoder;

    public ThemeController(IDirectoryService directoryService, IUnitOfWork unitOfWork, HtmlEncoder htmlEncoder)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _htmlEncoder = htmlEncoder;
    }

    /// <summary>
    /// Returns css content to the UI. UI is expected to escape the content
    /// </summary>
    /// <returns></returns>
    [HttpGet("download-content")]
    public ActionResult<string> GetThemeContent(int themeId)
    {
        var text = System.IO.File.ReadAllText(
            _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, "custom.css"));
        return Ok(text);
    }
}
