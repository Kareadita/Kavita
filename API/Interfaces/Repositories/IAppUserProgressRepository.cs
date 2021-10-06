using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;

namespace API.Interfaces.Repositories
{
    public interface IAppUserProgressRepository
    {
        void Update(AppUserProgress userProgress);
        Task<int> CleanupAbandonedChapters();
        Task<bool> UserHasProgress(LibraryType libraryType, int userId);
        Task<AppUserProgress> GetUserProgressAsync(int chapterId, int userId);
    }
}
