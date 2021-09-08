using System.Threading.Tasks;
using API.Entities.Enums;

namespace API.Interfaces.Repositories
{
    public interface IAppUserProgressRepository
    {
        Task<int> CleanupAbandonedChapters();
        Task<bool> UserHasProgress(LibraryType libraryType, int userId);
    }
}
