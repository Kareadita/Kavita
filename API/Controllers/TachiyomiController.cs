﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// All APIs are for Tachiyomi extension and app. They have hacks for our implementation and should not be used for any
/// other purposes.
/// </summary>
public class TachiyomiController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReaderService _readerService;

    public TachiyomiController(IUnitOfWork unitOfWork, IReaderService readerService)
    {
        _unitOfWork = unitOfWork;
        _readerService = readerService;
    }

    /// <summary>
    /// Given the series Id, this should return the latest chapter that has been fully read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("latest-chapter")]
    public async Task<ActionResult<ChapterDto>> GetLatestChapter(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());

        var currentChapter = await _readerService.GetContinuePoint(seriesId, userId);

        var prevChapterId =
            await _readerService.GetPrevChapterIdAsync(seriesId, currentChapter.VolumeId, currentChapter.Id, userId);

        // If prevChapterId is -1, this means either nothing is read or everything is read.
        if (prevChapterId == -1)
        {
            var userWithProgress = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Progress);
            var userHasProgress =
                userWithProgress.Progresses.Any(x => x.SeriesId == seriesId);

            // If the user doesn't have progress, then return null, which the extension will catch as 204 (no content) and report nothing as read
            if (!userHasProgress) return null;

            // Else return the max chapter to Tachiyomi so it can consider everything read
            var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(seriesId)).ToImmutableList();
            var looseLeafChapterVolume = volumes.FirstOrDefault(v => v.Number == 0);
            if (looseLeafChapterVolume == null)
            {
                return Ok(volumes.Last().Chapters.First());
            }

            var lastChapter = looseLeafChapterVolume.Chapters.OrderBy(c => float.Parse(c.Number), new ChapterSortComparer()).Last();
            return Ok(lastChapter.Number);
        }

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
            // When Tachiyomi sync's progress, if there is no current progress in Tachiyomi, 0.0f is sent.
            // Due to the encoding for volumes, this marks all chapters in volume 0 (loose chapters) as read.
            // Hence we catch and return early, so we ignore the request.
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
