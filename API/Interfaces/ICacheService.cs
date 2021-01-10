using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces
{
    public interface ICacheService
    {
        /// <summary>
        /// Ensures the cache is created for the given volume and if not, will create it. Should be called before any other
        /// cache operations (except cleanup).
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns>Volume for the passed volumeId. Side-effect from ensuring cache.</returns>
        Task<Volume> Ensure(int volumeId);

        bool Cleanup(Volume volume);

        //bool CleanupAll();

        /// <summary>
        /// Returns the absolute path of a cached page. 
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="page">Page number to look for</param>
        /// <returns></returns>
        string GetCachedPagePath(Volume volume, int page);
    }
}