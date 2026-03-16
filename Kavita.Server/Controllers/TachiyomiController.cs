using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// All APIs are for Tachiyomi extension and app. They have hacks for our implementation and should not be used for any
/// other purposes.
/// </summary>
public class TachiyomiController(
    IUnitOfWork unitOfWork,
    ITachiyomiService tachiyomiService,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Given the series Id, this should return the latest chapter that has been fully read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns>ChapterDTO of latest chapter. Only Chapter number is used by consuming app. All other fields may be missing.</returns>
    [HttpGet("latest-chapter")]
    public async Task<ActionResult<ChapterDto>> GetLatestChapter(int seriesId)
    {
        if (seriesId < 1) return BadRequest(await localizationService.Translate(UserId, "greater-0", "SeriesId"));
        return Ok(await tachiyomiService.GetLatestChapter(seriesId, UserId));
    }

    /// <summary>
    /// Marks every chapter that is sorted below the passed number as Read. This will not mark any specials as read.
    /// </summary>
    /// <remarks>This is built for Tachiyomi and is not expected to be called by any other place</remarks>
    /// <returns></returns>
    [HttpPost("mark-chapter-until-as-read")]
    public async Task<ActionResult<bool>> MarkChaptersUntilAsRead(int seriesId, float chapterNumber)
    {
        var user = (await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
            AppUserIncludes.Progress))!;
        return Ok(await tachiyomiService.MarkChaptersUntilAsRead(user, seriesId, chapterNumber));
    }
}
