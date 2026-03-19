using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;

namespace Kavita.API.Repositories;

public interface IAppUserProgressRepository
{
    void Update(AppUserProgress userProgress);
    void Remove(AppUserProgress userProgress);
    Task<int> CleanupAbandonedChapters(CancellationToken ct = default);
    Task<bool> UserHasProgress(LibraryType libraryType, int userId, CancellationToken ct = default);
    Task<AppUserProgress?> GetUserProgressAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<bool> HasAnyProgressOnSeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<IEnumerable<AppUserProgress>> GetUserProgressForSeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<IEnumerable<AppUserProgress>> GetAllProgress(CancellationToken ct = default);
    Task<DateTime> GetLatestProgress(CancellationToken ct = default);
    Task<ProgressDto?> GetUserProgressDtoAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<bool> AnyUserProgressForSeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<int> GetHighestFullyReadChapterForSeries(int seriesId, int userId, CancellationToken ct = default);
    Task<float> GetHighestFullyReadVolumeForSeries(int seriesId, int userId, CancellationToken ct = default);
    Task<DateTime?> GetLatestProgressForSeries(int seriesId, int userId, CancellationToken ct = default);
    Task<DateTime?> GetLatestProgressForVolume(int volumeId, int userId, CancellationToken ct = default);
    Task<DateTime?> GetLatestProgressForChapter(int chapterId, int userId, CancellationToken ct = default);
    Task<DateTime?> GetFirstProgressForSeries(int seriesId, int userId, CancellationToken ct = default);
    Task<DateTime?> GetFirstProgressForUser(int userId, CancellationToken ct = default);
    Task UpdateAllProgressThatAreMoreThanChapterPages(CancellationToken ct = default);
    Task<IList<FullProgressDto>> GetUserProgressForChapter(int chapterId, int userId = 0, CancellationToken ct = default);

    Task<Dictionary<int, int>> GetUserProgressForChaptersBySeries(int userId, int seriesId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetUserProgressForChaptersByVolumes(int userId, int seriesId, List<int> volumeIds, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetUserProgressForChaptersByChapters(int userId, int seriesId, List<int> chapterIds, CancellationToken ct = default);
}
