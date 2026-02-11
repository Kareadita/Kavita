using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface ITachiyomiService
{
    Task<TachiyomiChapterDto?> GetLatestChapter(int seriesId, int userId);
    Task<bool> MarkChaptersUntilAsRead(AppUser userWithProgress, int seriesId, float chapterNumber);
}
