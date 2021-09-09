using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces.Services
{
    public interface IReaderService
    {
        Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
        Task<int> CapPageToChapter(int chapterId, int page);
        Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
        Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    }
}
