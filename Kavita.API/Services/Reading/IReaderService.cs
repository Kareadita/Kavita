using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Reading;

public interface IReaderService
{
    public const float MinWordsPerHour = 10260F;
    public const float MaxWordsPerHour = 30000F;
    public const float MinPagesPerMinute = 3.33F;
    public const float MaxPagesPerMinute = 2.75F;
    public const float AvgWordsPerHour = (MaxWordsPerHour + MinWordsPerHour) / 2F;
    public const float AvgPagesPerMinute = (MaxPagesPerMinute + MinPagesPerMinute) / 2F; //3.04

    Task MarkSeriesAsRead(AppUser user, int seriesId);
    Task MarkSeriesAsUnread(AppUser user, int seriesId);
    Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters);
    Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId, bool saveToReadingSession = true);
    int CapPageToChapter(Chapter chapter, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId);
    Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber);
    Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber);
    IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions);
    Task<string> GetThumbnail(Chapter chapter, int pageNum, IEnumerable<string> cachedImages);
    Task<RereadDto> CheckSeriesForReRead(int userId, int seriesId, int libraryId);
    Task<RereadDto> CheckVolumeForReRead(int userId, int volumeId, int seriesId, int libraryId);
    Task<RereadDto> CheckChapterForReRead(int userId, int chapterId, int seriesId, int libraryId);
    Task<HourEstimateRangeDto> GetEstimateToCompletionForChapter(int userId, int seriesId, int chapterId);
    Task<HourEstimateRangeDto> GetEstimateFromPageForChapter(int userId, int seriesId, int chapterId, int page);
}
