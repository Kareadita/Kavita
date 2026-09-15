using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services.Reading;
using Kavita.Models.DTOs.Progress;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// For the Panels app explicitly
/// </summary>
public class PanelsController(IReaderService readerService, IUnitOfWork unitOfWork) : BaseApiController
{
    /// <summary>
    /// Saves the progress of a given chapter. This will generate a reading session with the estimated time from the
    /// last progress till the current
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpPost("save-progress")]
    public async Task<ActionResult> SaveProgress(ProgressDto dto, [FromQuery] string apiKey)
    {
        var ct = HttpContext.RequestAborted;
        if (!await unitOfWork.UserRepository.HasAccessToChapter(UserId, dto.ChapterId, ct)) // TODO: Use [ChapterAccess] instead?
            return NotFound();

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId, ct: ct);
        if (chapter == null) return NotFound();

        var progressMap = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersByChapters(UserId, dto.SeriesId, [dto.ChapterId], ct);

        await readerService.SaveReadingProgress(dto, UserId, false, ct);

        BackgroundJob.Enqueue<IReadingSessionService>(s
            => s.GenerateReadingSessionForChapters(UserId, dto.SeriesId, progressMap, CancellationToken.None));

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
        var ct = HttpContext.RequestAborted;
        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, UserId, ct);
        if (progress == null) return Ok(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = chapterId,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0
        });
        return Ok(progress);
    }
}
