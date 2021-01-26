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

        /// <summary>
        /// Clears cache directory of all folders and files.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Clears cache directory of all volumes. This can be invoked from deleting a library or a series.
        /// </summary>
        /// <param name="volumeIds">Volumes that belong to that library. Assume the library might have been deleted before this invocation.</param>
        void CleanupVolumes(int[] volumeIds);
        

        /// <summary>
        /// Returns the absolute path of a cached page. 
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="page">Page number to look for</param>
        /// <returns></returns>
        string GetCachedPagePath(Volume volume, int page);

        bool CacheDirectoryIsAccessible();
    }
}