using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Services
{
    public interface IReaderService
    {
        void MarkChaptersAsRead(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
        void MarkChaptersAsUnread(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
        Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
        Task<int> CapPageToChapter(int chapterId, int page);
        Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
        Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    }
}
