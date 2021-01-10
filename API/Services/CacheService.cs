using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<Volume> Ensure(int volumeId)
        {
            Volume volume = await _seriesRepository.GetVolumeAsync(volumeId);
            foreach (var file in volume.Files)
            {
                var extractPath = GetVolumeCachePath(volumeId, file);

                _directoryService.ExtractArchive(file.FilePath, extractPath);
            }

            return volume;
        }
        

        public bool Cleanup(Volume volume)
        {
            throw new System.NotImplementedException();
        }

        private string GetVolumeCachePath(int volumeId, MangaFile file)
        {
            var extractPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), $"../cache/{volumeId}/"));
            if (file.Chapter > 0)
            {
                extractPath = Path.Join(extractPath, file.Chapter + "");
            }
            return extractPath;
        }

        public string GetCachedPagePath(Volume volume, int page)
        {
            // Calculate what chapter the page belongs to
            foreach (var mangaFile in volume.Files.OrderBy(f => f.Chapter))
            {
                if (page + 1 < mangaFile.NumberOfPages)
                {
                    return GetVolumeCachePath(volume.Id, mangaFile);
                }
            }
            return "";
        }
    }
}