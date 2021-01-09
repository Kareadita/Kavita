using API.Entities;

namespace API.Interfaces
{
    public interface ICacheService
    {
        /// <summary>
        /// Ensures the cache is created for the given volume and if not, will create it.
        /// </summary>
        /// <param name="volumeId"></param>
        void Ensure(int volumeId);

        bool Cleanup(Volume volume);

        //bool CleanupAll();

        string GetCachePath(int volumeId);
        
        
    }
}