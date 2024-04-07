using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Progress;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

/// <summary>
/// For the Panels app explicitly
/// </summary>
[AllowAnonymous]
public class PanelsController : BaseApiController
{
    private readonly IReaderService _readerService;
    private readonly IUnitOfWork _unitOfWork;

    public PanelsController(IReaderService readerService, IUnitOfWork unitOfWork)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Saves the progress of a given chapter.
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpPost("save-progress")]
    public async Task<ActionResult> SaveProgress(ProgressDto dto, [FromQuery] string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return Unauthorized("ApiKey is required");
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        await _readerService.SaveReadingProgress(dto, userId);
        return Ok();
    }

    /// <summary>
    /// Gets the Progress of a given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="apiKey"></param>
    /// <returns>The number of pages read, 0 if none read</returns>
    [HttpGet("get-progress")]
    public async Task<ActionResult<ProgressDto>> GetProgress(int chapterId, [FromQuery] string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return Unauthorized("ApiKey is required");
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);

        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, userId);
        if (progress == null) return Ok(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = chapterId,
            VolumeId = 0,
            SeriesId = 0,
        });
        return Ok(progress);
    }
}
