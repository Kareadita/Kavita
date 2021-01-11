using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDirectoryService _directoryService;
        private readonly ISeriesRepository _seriesRepository;
        private readonly ILogger<CacheService> _logger;
        private readonly NumericComparer _numericComparer;
        private readonly string _cacheDirectory = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../cache/"));

        public CacheService(IDirectoryService directoryService, ISeriesRepository seriesRepository, ILogger<CacheService> logger)
        {
            _directoryService = directoryService;
            _seriesRepository = seriesRepository;
            _logger = logger;
            _numericComparer = new NumericComparer();
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
        

        public void Cleanup()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            
            DirectoryInfo di = new DirectoryInfo(_cacheDirectory);

            try
            {
                di.Empty();
            }
            catch (Exception ex)
            {
                _logger.LogError("There was an issue deleting one or more folders/files during cleanup.", ex);
            }
            
            _logger.LogInformation("Cache directory purged.");
        }
        
        public void CleanupLibrary(int libraryId, int[] volumeIds)
        {
            _logger.LogInformation($"Running Cache cleanup on Library: {libraryId}");
            
            foreach (var volume in volumeIds)
            {
                var di = new DirectoryInfo(Path.Join(_cacheDirectory, volume + ""));
                if (di.Exists)
                {
                    di.Delete(true);    
                }
                
            }
            _logger.LogInformation("Cache directory purged");
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
            var pagesSoFar = 0;
            foreach (var mangaFile in volume.Files.OrderBy(f => f.Chapter))
            {
                if (page + 1 < (mangaFile.NumberOfPages + pagesSoFar))
                {
                    var path = GetVolumeCachePath(volume.Id, mangaFile);
                    
                    var files = _directoryService.ListFiles(path);
                    var array = files.ToArray();
                    Array.Sort(array, _numericComparer); // TODO: Find a way to apply numericComparer to IList.
                    
                    return array.ElementAt((page + 1) - pagesSoFar);
                }

                pagesSoFar += mangaFile.NumberOfPages;
            }
            return "";
        }
    }
}