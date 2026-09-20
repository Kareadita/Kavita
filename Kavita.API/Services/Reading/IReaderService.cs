using System.Collections.Generic;
using System.Threading;
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

    Task MarkSeriesAsRead(AppUser user, int seriesId, CancellationToken ct = default);
    Task MarkSeriesAsUnread(AppUser user, int seriesId, CancellationToken ct = default);
    Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters, CancellationToken ct = default);
    Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters, CancellationToken ct = default);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId, bool saveToReadingSession = true, CancellationToken ct = default);
    int CapPageToChapter(Chapter chapter, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId, CancellationToken ct = default);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId, CancellationToken ct = default);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId, CancellationToken ct = default);
    IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions);
    Task<string> GetThumbnail(Chapter chapter, int pageNum, IEnumerable<string> cachedImages, CancellationToken ct = default);
    Task<RereadDto> CheckSeriesForReRead(int userId, int seriesId, int libraryId, CancellationToken ct = default);
    Task<RereadDto> CheckVolumeForReRead(int userId, int volumeId, int seriesId, int libraryId, CancellationToken ct = default);
    Task<RereadDto> CheckChapterForReRead(int userId, int chapterId, int seriesId, int libraryId, CancellationToken ct = default);
    Task<HourEstimateRangeDto> GetEstimateToCompletionForChapter(int userId, int seriesId, int chapterId, CancellationToken ct = default);
    Task<HourEstimateRangeDto> GetEstimateFromPageForChapter(int userId, int seriesId, int chapterId, int page, CancellationToken ct = default);
}
