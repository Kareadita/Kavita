using System.IO;
using API.Entities;
using API.Interfaces;

namespace API.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDirectoryService _directoryService;
        private readonly ISeriesRepository _seriesRepository;

        public CacheService(IDirectoryService directoryService, ISeriesRepository seriesRepository)
        {
            _directoryService = directoryService;
            _seriesRepository = seriesRepository;
        }

        public async void Ensure(int volumeId)
        {
            Volume volume = await _seriesRepository.GetVolumeAsync(volumeId);
            foreach (var file in volume.Files)
            {
                var extractPath = GetCachePath(volumeId);
                if (file.Chapter > 0)
                {
                    extractPath = Path.Join(extractPath, file.Chapter + "");
                }
                
                _directoryService.ExtractArchive(file.FilePath, extractPath);
            }
        }

        public bool Cleanup(Volume volume)
        {
            throw new System.NotImplementedException();
        }

        public string GetCachePath(int volumeId)
        {
            // TODO: Make this an absolute path, no ..'s in it.
            return Path.Join(Directory.GetCurrentDirectory(), $"../cache/{volumeId}/");
        }
        
        
    }
}