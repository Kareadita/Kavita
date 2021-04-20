using System.Threading.Tasks;
using API.Entities.Enums;

namespace API.Interfaces
{
    public interface IAppUserProgressRepository
    {
        Task<int> CleanupAbandonedChapters();
        Task<bool> UserHasProgress(LibraryType libraryType);
    }
}