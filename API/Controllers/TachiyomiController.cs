using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

/// <summary>
/// All APIs are for Tachiyomi extension and app. They have hacks for our implementation and should not be used for any
/// other purposes.
/// </summary>
public class TachiyomiController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITachiyomiService _tachiyomiService;
    private readonly ILocalizationService _localizationService;

    public TachiyomiController(IUnitOfWork unitOfWork, ITachiyomiService tachiyomiService,
        ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _tachiyomiService = tachiyomiService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Given the series Id, this should return the latest chapter that has been fully read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns>ChapterDTO of latest chapter. Only Chapter number is used by consuming app. All other fields may be missing.</returns>
    [HttpGet("latest-chapter")]
    public async Task<ActionResult<ChapterDto>> GetLatestChapter(int seriesId)
    {
        if (seriesId < 1) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "SeriesId"));
        return Ok(await _tachiyomiService.GetLatestChapter(seriesId, User.GetUserId()));
    }

    /// <summary>
    /// Marks every chapter that is sorted below the passed number as Read. This will not mark any specials as read.
    /// </summary>
    /// <remarks>This is built for Tachiyomi and is not expected to be called by any other place</remarks>
    /// <returns></returns>
    [HttpPost("mark-chapter-until-as-read")]
    public async Task<ActionResult<bool>> MarkChaptersUntilAsRead(int seriesId, float chapterNumber)
    {
        var user = (await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
            AppUserIncludes.Progress))!;
        return Ok(await _tachiyomiService.MarkChaptersUntilAsRead(user, seriesId, chapterNumber));
    }
}
