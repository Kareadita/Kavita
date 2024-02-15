using System;
using API.DTOs;
using System.Threading.Tasks;
using API.Data;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface ITachiyomiService
{
    Task<TachiyomiChapterDto?> GetLatestChapter(int seriesId, int userId);
    Task<bool> MarkChaptersUntilAsRead(AppUser userWithProgress, int seriesId, float chapterNumber);
}

/// <summary>
/// All APIs are for Tachiyomi extension and app. They have hacks for our implementation and should not be used for any
/// other purposes.
/// </summary>
public class TachiyomiService : ITachiyomiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ReaderService> _logger;
    private readonly IReaderService _readerService;

    private static readonly CultureInfo EnglishCulture = CultureInfo.CreateSpecificCulture("en-US");

    public TachiyomiService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ReaderService> logger, IReaderService readerService)
    {
        _unitOfWork = unitOfWork;
        _readerService = readerService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest chapter/volume read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns>Due to how Tachiyomi works we need a hack to properly return both chapters and volumes.
    /// If its a chapter, return the chapterDto as is.
    /// If it's a volume, the volume number gets returned in the 'Number' attribute of a chapterDto encoded.
    /// The volume number gets divided by 10,000 because that's how Tachiyomi interprets volumes</returns>
    public async Task<TachiyomiChapterDto?> GetLatestChapter(int seriesId, int userId)
    {
        var currentChapter = await _readerService.GetContinuePoint(seriesId, userId);

        var prevChapterId =
            await _readerService.GetPrevChapterIdAsync(seriesId, currentChapter.VolumeId, currentChapter.Id, userId);

        // If prevChapterId is -1, this means either nothing is read or everything is read.
        if (prevChapterId == -1)
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
            var userHasProgress = series.PagesRead != 0 && series.PagesRead <= series.Pages;

            // If the user doesn't have progress, then return null, which the extension will catch as 204 (no content) and report nothing as read
            if (!userHasProgress) return null;

            // Else return the max chapter to Tachiyomi so it can consider everything read
            var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(seriesId)).ToImmutableList();
            var looseLeafChapterVolume = volumes.GetLooseLeafVolumeOrDefault();
            if (looseLeafChapterVolume == null)
            {
                var volumeChapter = _mapper.Map<ChapterDto>(volumes
                    [^1].Chapters
                    .OrderBy(c => c.MinNumber, ChapterSortComparerZeroFirst.Default)
                    .Last());
                if (volumeChapter.Number == Parser.LooseLeafVolume)
                {
                    var volume = volumes.First(v => v.Id == volumeChapter.VolumeId);
                    return new TachiyomiChapterDto()
                    {
                        // Use R to ensure that localization of underlying system doesn't affect the stringification
                        // https://docs.microsoft.com/en-us/globalization/locale/number-formatting-in-dotnet-framework
                        Number = (((int) volume.MinNumber) / 10_000f).ToString("R", EnglishCulture)
                    };
                }

                return new TachiyomiChapterDto()
                {
                    Number = (int.Parse(volumeChapter.Number) / 10_000f).ToString("R", EnglishCulture)
                };
            }

            var lastChapter = looseLeafChapterVolume.Chapters
                .OrderBy(c => c.MinNumber, ChapterSortComparer.Default)
                .Last();
            return _mapper.Map<TachiyomiChapterDto>(lastChapter);
        }

        // There is progress, we now need to figure out the highest volume or chapter and return that.
        var prevChapter = (TachiyomiChapterDto) (await _unitOfWork.ChapterRepository.GetChapterDtoAsync(prevChapterId))!;

        var volumeWithProgress = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(prevChapter.VolumeId, userId);
        // We only encode for single-file volumes
        if (!volumeWithProgress!.IsLooseLeaf() && volumeWithProgress.Chapters.Count == 1)
        {
            // The progress is on a volume, encode it as a fake chapterDTO
            return new TachiyomiChapterDto()
            {
                // Use R to ensure that localization of underlying system doesn't affect the stringification
                // https://docs.microsoft.com/en-us/globalization/locale/number-formatting-in-dotnet-framework
                Number = (volumeWithProgress.MinNumber / 10_000f).ToString("R", EnglishCulture)

            };
        }

        // Progress is just on a chapter, return as is
        return prevChapter;
    }

    /// <summary>
    /// Marks every chapter and volume that is sorted below the passed number as Read. This will not mark any specials as read.
    /// Passed number will also be marked as read
    /// </summary>
    /// <param name="userWithProgress"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterNumber">Can also be a Tachiyomi encoded volume number</param>
    public async Task<bool> MarkChaptersUntilAsRead(AppUser userWithProgress, int seriesId, float chapterNumber)
    {
        userWithProgress.Progresses ??= new List<AppUserProgress>();

        switch (chapterNumber)
        {
            // When Tachiyomi sync's progress, if there is no current progress in Tachiyomi, 0.0f is sent.
            // Due to the encoding for volumes, this marks all chapters in volume 0 (loose chapters) as read.
            // Hence we catch and return early, so we ignore the request.
            case 0.0f:
                return true;
            case < 1.0f:
            {
                // This is a hack to track volume number. We need to map it back by x10,000
                var volumeNumber = int.Parse($"{(int)(chapterNumber * 10_000)}", EnglishCulture);
                await _readerService.MarkVolumesUntilAsRead(userWithProgress, seriesId, volumeNumber);
                break;
            }
            default:
                await _readerService.MarkChaptersUntilAsRead(userWithProgress, seriesId, chapterNumber);
                break;
        }

        try {
            _unitOfWork.UserRepository.Update(userWithProgress);

            if (!_unitOfWork.HasChanges()) return true;
            if (await _unitOfWork.CommitAsync()) return true;
        } catch (Exception ex) {
            _logger.LogError(ex, "There was an error saving progress from tachiyomi");
            await _unitOfWork.RollbackAsync();
        }
        return false;
    }
}
