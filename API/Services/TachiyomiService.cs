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
using AutoMapper;
namespace API.Services;

public interface ITachiyomiService
{
    Task<ChapterDto> GetLatestChapter(int seriesId, int userId);
    Task<bool> MarkChaptersUntilAsRead(AppUser userWithProgress, int seriesId, float chapterNumber);
}


public class TachiyomiService : ITachiyomiService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReaderService _readerService;
    private readonly IMapper _mapper;
    private readonly CultureInfo _englishCulture = CultureInfo.CreateSpecificCulture("en-US");

    public TachiyomiService(IUnitOfWork unitOfWork,IMapper mapper,IReaderService readerService)
    {
        _unitOfWork = unitOfWork;
        _readerService = readerService;
        _mapper = mapper;
    }

    public async Task<ChapterDto> GetLatestChapter(int seriesId,int userId)
//    public async Task<ChapterDto> GetLatestChapter(int seriesId, int userId)
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
            var looseLeafChapterVolume = volumes.FirstOrDefault(v => v.Number == 0);
            if (looseLeafChapterVolume == null)
            {
                var volumeChapter = _mapper.Map<ChapterDto>(volumes.Last().Chapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparerZeroFirst.Default).Last());
                if (volumeChapter.Number == "0")
                {
                    var volume = volumes.First(v => v.Id == volumeChapter.VolumeId);
                    return new ChapterDto()
                    {
                        Number = (volume.Number / 10000f).ToString("R", _englishCulture)
                    };
                }

                return new ChapterDto()
                {
                    Number = (int.Parse(volumeChapter.Number) / 10000f).ToString("R", _englishCulture)
                    //Number = $"{int.Parse(volumeChapter.Number) / 1000f}"
                };
            }

            var lastChapter = looseLeafChapterVolume.Chapters.OrderBy(c => float.Parse(c.Number), ChapterSortComparer.Default).Last();
            return _mapper.Map<ChapterDto>(lastChapter);
        }

        // There is progress, we now need to figure out the highest volume or chapter and return that.
        var prevChapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(prevChapterId);
        var volumeWithProgress = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(prevChapter.VolumeId, userId);
        // We only encode for single-file volumes
        if (volumeWithProgress.Number != 0 && volumeWithProgress.Chapters.Count == 1)
        {
            // The progress is on a volume, encode it as a fake chapterDTO
            return new ChapterDto()
            {
                // Use R to ensure that localization of underlying system doesn't affect the stringification
                // https://docs.microsoft.com/en-us/globalization/locale/number-formatting-in-dotnet-framework
                Number = (volumeWithProgress.Number / 10000f).ToString("R", _englishCulture)

            };
        }

        // Progress is just on a chapter, return as is
        return prevChapter;
    }

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
                // This is a hack to track volume number. We need to map it back by x100
                var chapterString = $"{chapterNumber}";
                var volumeNumber = int.Parse($"{(int)(chapterNumber * 10000)}", _englishCulture);
                //var volumeNumber = int.Parse($"{float.Parse(chapterString.Substring(0, Math.Min(7, chapterString.Length))) * 100f}");
                await _readerService.MarkVolumesUntilAsRead(userWithProgress, seriesId, volumeNumber);
                break;
            }
            default:
                await _readerService.MarkChaptersUntilAsRead(userWithProgress, seriesId, chapterNumber);
                break;
        }


        _unitOfWork.UserRepository.Update(userWithProgress);

        if (!_unitOfWork.HasChanges()) return true;
        if (await _unitOfWork.CommitAsync()) return true;

        await _unitOfWork.RollbackAsync();
        return false;
    }
}
