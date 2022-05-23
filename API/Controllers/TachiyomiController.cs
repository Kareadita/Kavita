using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class TachiyomiController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReaderService _readerService;

    public TachiyomiController(IUnitOfWork unitOfWork, IReaderService readerService)
    {
        _unitOfWork = unitOfWork;
        _readerService = readerService;
    }

    [HttpGet("latest-chapter")]
    public async Task<ActionResult<ChapterDto>> GetLatestChapter(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());

        var currentChapter = await _readerService.GetContinuePoint(seriesId, userId);

        var prevChapterId =
            await _readerService.GetPrevChapterIdAsync(seriesId, currentChapter.VolumeId, currentChapter.Id, userId);

        if (prevChapterId == -1) return null;

        var prevChapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(prevChapterId);

        return Ok(prevChapter);
    }

    /// <summary>
    /// Marks every chapter that is sorted below the passed number as Read. This will not mark any specials as read.
    /// </summary>
    /// <remarks>This is built for Tachiyomi and is not expected to be called by any other place</remarks>
    /// <returns></returns>
    [HttpPost("mark-chapter-until-as-read")]
    public async Task<ActionResult<bool>> MarkChaptersUntilAsRead(int seriesId, float chapterNumber)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        user.Progresses ??= new List<AppUserProgress>();

        switch (chapterNumber)
        {
            // Tachiyomi sends chapter 0.0f when there's no chapters read.
            // Due to the encoding for volumes this marks all chapters in volume 0 (loose chapters) as read so we ignore it
            case 0.0f:
                return true;
            case < 1.0f:
            {
                // This is a hack to track volume number. We need to map it back by x100
                var volumeNumber = int.Parse($"{chapterNumber * 100f}");
                await _readerService.MarkVolumesUntilAsRead(user, seriesId, volumeNumber);
                break;
            }
            default:
                await _readerService.MarkChaptersUntilAsRead(user, seriesId, chapterNumber);
                break;
        }


        _unitOfWork.UserRepository.Update(user);

        if (!_unitOfWork.HasChanges()) return Ok(true);
        if (await _unitOfWork.CommitAsync()) return Ok(true);

        await _unitOfWork.RollbackAsync();
        return Ok(false);
    }
}
