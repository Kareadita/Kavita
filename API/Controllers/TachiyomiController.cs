using System.Collections.Generic;
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
using AutoMapper;
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
    private readonly IMapper _mapper;

    public TachiyomiController(IUnitOfWork unitOfWork, IReaderService readerService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _readerService = readerService;
        _mapper = mapper;
    }

    /// <summary>
    /// Given the series Id, this should return the latest chapter that has been fully read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns>ChapterDTO of latest chapter. Only Chapter number is used by consuming app. All other fields may be missing.</returns>
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
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
            var userHasProgress = series.PagesRead != 0 && series.PagesRead < series.Pages;

            // If the user doesn't have progress, then return null, which the extension will catch as 204 (no content) and report nothing as read
            if (!userHasProgress) return null;

            // Else return the max chapter to Tachiyomi so it can consider everything read
            var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(seriesId)).ToImmutableList();
            var looseLeafChapterVolume = volumes.FirstOrDefault(v => v.Number == 0);
            if (looseLeafChapterVolume == null)
            {
                var volumeChapter = _mapper.Map<ChapterDto>(volumes.Last().Chapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparerZeroFirst.Default).Last());
                return Ok(new ChapterDto()
                {
                    Number = $"{int.Parse(volumeChapter.Number) / 100f}"
                });
            }

            var lastChapter = looseLeafChapterVolume.Chapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default).Last();
            return Ok(_mapper.Map<ChapterDto>(lastChapter));
        }

        // There is progress, we now need to figure out the highest volume or chapter and return that.
        var prevChapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(prevChapterId);
        var volumeWithProgress = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(prevChapter.VolumeId, userId);
        // We only encode for single-file volumes
        if (volumeWithProgress.Number != 0 && volumeWithProgress.Chapters.Count == 1)
        {
            // The progress is on a volume, encode it as a fake chapterDTO
            return Ok(new ChapterDto()
            {
                Number = $"{volumeWithProgress.Number / 100f}"
            });
        }

        // Progress is just on a chapter, return as is
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
