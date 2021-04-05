using System.Threading.Tasks;

namespace API.Interfaces
{
    public interface IAppUserProgressRepository
    {
        Task<bool> CleanupAbandonedChapters();
    }
}