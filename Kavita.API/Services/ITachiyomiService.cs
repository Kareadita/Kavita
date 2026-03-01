using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface ITachiyomiService
{
    /// <summary>
    /// Gets the latest chapter/volume read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns>Due to how Tachiyomi works we need a hack to properly return both chapters and volumes.
    /// If its a chapter, return the chapterDto as is.
    /// If it's a volume, the volume number gets returned in the 'Number' attribute of a chapterDto encoded.
    /// The volume number gets divided by 10,000 because that's how Tachiyomi interprets volumes</returns>
    Task<TachiyomiChapterDto?> GetLatestChapter(int seriesId, int userId, CancellationToken ct = default);
    /// <summary>
    /// Marks every chapter and volume that is sorted below the passed number as Read. This will not mark any specials as read.
    /// Passed number will also be marked as read
    /// </summary>
    /// <param name="userWithProgress"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterNumber">Can also be a Tachiyomi encoded volume number</param>
    /// <param name="ct"></param>
    Task<bool> MarkChaptersUntilAsRead(AppUser userWithProgress, int seriesId, float chapterNumber, CancellationToken ct = default);
}
