using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces
{
    public interface ICacheService
    {
        /// <summary>
        /// Ensures the cache is created for the given chapter and if not, will create it. Should be called before any other
        /// cache operations (except cleanup).
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns>Chapter for the passed chapterId. Side-effect from ensuring cache.</returns>
        Task<Chapter> Ensure(int chapterId);

        /// <summary>
        /// Clears cache directory of all folders and files.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Clears cache directory of all volumes. This can be invoked from deleting a library or a series.
        /// </summary>
        /// <param name="chapterIds">Volumes that belong to that library. Assume the library might have been deleted before this invocation.</param>
        void CleanupChapters(int[] chapterIds);
        

        /// <summary>
        /// Returns the absolute path of a cached page. 
        /// </summary>
        /// <param name="chapter">Chapter entity with Files populated.</param>
        /// <param name="page">Page number to look for</param>
        /// <returns></returns>
        Task<(string path, MangaFile file)> GetCachedPagePath(Chapter chapter, int page);

        void EnsureCacheDirectory();
    }
}