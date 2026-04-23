using System;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Comparators;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

/// <summary>
/// All APIs are for Tachiyomi extension and app. They have hacks for our implementation and should not be used for any
/// other purposes.
/// </summary>
public class TachiyomiService(
    IUnitOfWork unitOfWork,
    IMapper mapper,
    ILogger<TachiyomiService> logger,
    IReaderService readerService)
    : ITachiyomiService
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.CreateSpecificCulture("en-US");

    public async Task<TachiyomiChapterDto?> GetLatestChapter(int seriesId, int userId, CancellationToken ct = default)
    {
        var currentChapter = await readerService.GetContinuePoint(seriesId, userId);

        var prevChapterId =
            await readerService.GetPrevChapterIdAsync(seriesId, currentChapter.VolumeId, currentChapter.Id, userId);

        // If prevChapterId is -1, this means either nothing is read or everything is read.
        if (prevChapterId == -1)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId, ct);
            var userHasProgress = series.PagesRead != 0 && series.PagesRead <= series.Pages;

            // If the user doesn't have progress, then return null, which the extension will catch as 204 (no content) and report nothing as read
            if (!userHasProgress) return null;

            // Else return the max chapter to Tachiyomi so it can consider everything read
            var volumes = (await unitOfWork.VolumeRepository.GetVolumes(seriesId, ct)).ToImmutableList();
            var looseLeafChapterVolume = volumes.GetLooseLeafVolumeOrDefault();
            if (looseLeafChapterVolume == null)
            {
                var volumeChapter = mapper.Map<ChapterDto>(volumes
                    [^1].Chapters
                    .OrderBy(c => c.MinNumber, ChapterSortComparerDefaultFirst.Default)
                    .Last());

                if (volumeChapter.MinNumber.Is(Parser.LooseLeafVolumeNumber))
                {
                    var volume = volumes.First(v => v.Id == volumeChapter.VolumeId);
                    return CreateTachiyomiChapterDto(volume.MinNumber);
                }

                return CreateTachiyomiChapterDto(volumeChapter.MinNumber);
            }

            var lastChapter = looseLeafChapterVolume.Chapters
                .OrderBy(c => c.MinNumber, ChapterSortComparerDefaultLast.Default)
                .Last();

            return mapper.Map<TachiyomiChapterDto>(lastChapter);
        }

        // There is progress, we now need to figure out the highest volume or chapter and return that.
        var prevChapter = (await unitOfWork.ChapterRepository.GetChapterDtoAsync(prevChapterId, userId, ct))!;

        var volumeWithProgress = (await unitOfWork.VolumeRepository.GetVolumeDtoAsync(prevChapter.VolumeId, userId, ct))!;
        // We only encode for single-file volumes
        if (!volumeWithProgress.IsLooseLeaf() && volumeWithProgress.Chapters.Count == 1)
        {
            // The progress is on a volume, encode it as a fake chapterDTO
            return CreateTachiyomiChapterDto(volumeWithProgress.MinNumber);
        }

        // Progress is just on a chapter, return as is
        return mapper.Map<TachiyomiChapterDto>(prevChapter);
    }

    private static TachiyomiChapterDto CreateTachiyomiChapterDto(float number)
    {
        return new TachiyomiChapterDto()
        {
            // Use R to ensure that localization of underlying system doesn't affect the stringification
            // https://docs.microsoft.com/en-us/globalization/locale/number-formatting-in-dotnet-framework
            Number = (number / 10_000f).ToString("R", EnglishCulture),
            Files = new List<MangaFileDto>()
        };
    }

    public async Task<bool> MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber,
        CancellationToken ct = default)
    {
        user.Progresses ??= [];

        var chapters = chapterNumber switch
        {
            // When Tachiyomi sync's progress, if there is no current progress in Tachiyomi, 0.0f is sent.
            // Due to the encoding for volumes, this marks all chapters in volume 0 (loose chapters) as read.
            // Hence we catch and return early, so we ignore the request.
            0.0f => [],
            // This is a hack to track volume number. We need to map it back by x10,000
            < 1.0f => await GetChaptersUntilVolume(seriesId, int.Parse($"{(int)(chapterNumber * 10_000)}", EnglishCulture)),
            _ => await GetChaptersUntilChapter(seriesId, chapterNumber)
        };

        if (chapters.Count == 0) return true;

        var chapterIds = chapters.Select(c => c.Id).ToList();

        var progressDictionary = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersByChapters(user.Id, seriesId, chapterIds, ct);

        await readerService.MarkChaptersAsRead(user, seriesId, chapters);

        // Generate reading sessions
        BackgroundJob.Enqueue<IReadingSessionService>(s
            => s.GenerateReadingSessionForChapters(user.Id, seriesId, progressDictionary, CancellationToken.None));

        try {
            if (!unitOfWork.HasChanges()) return true;
            if (await unitOfWork.CommitAsync(ct)) return true;
        } catch (Exception ex) {
            logger.LogError(ex, "There was an error saving progress from tachiyomi");
            await unitOfWork.RollbackAsync(ct);
        }
        return false;
    }

    private async Task<List<Chapter>> GetChaptersUntilVolume(int seriesId, int volumeNumber)
    {
        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync([seriesId], true);

        return volumes
            .Where(v => v.MinNumber <= volumeNumber && v.MinNumber > 0)
            .OrderBy(v => v.MinNumber)
            .SelectMany(v => v.Chapters)
            .ToList();
    }

    private async Task<List<Chapter>> GetChaptersUntilChapter(int seriesId, float chapterNumber)
    {
        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync([seriesId], true);

        return volumes
            .OrderBy(v => v.MinNumber)
            .SelectMany(v => v.Chapters)
            .Where(c => !c.IsSpecial && c.MaxNumber <= chapterNumber)
            .OrderBy(c => c.MinNumber)
            .ToList();
    }


}
