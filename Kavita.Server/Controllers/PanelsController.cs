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
public class PanelsController(IReaderService readerService, IUnitOfWork unitOfWork, IReadingSessionService readingSessionService) : BaseApiController
{
    /// <summary>
    /// Saves the progress of a given chapter.
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpPost("save-progress")]
    public async Task<ActionResult> SaveProgress(ProgressDto dto, [FromQuery] string apiKey)
    {
        if (!await unitOfWork.UserRepository.HasAccessToChapter(UserId, dto.ChapterId))
            return NotFound();

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId);
        if (chapter == null) return NotFound();

        if (dto.PageNum >= chapter.Pages)
        {
            await readingSessionService.GenerateReadingSessionForChapters(UserId, dto.SeriesId, [dto.ChapterId], CancellationToken.None);
        }

        await readerService.SaveReadingProgress(dto, UserId);
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
        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, UserId);
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
