using System;
using System.Collections.Generic;
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
        private readonly ILogger<CacheService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;
        private readonly NumericComparer _numericComparer;
        public static readonly string CacheDirectory = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../cache/"));

        public CacheService(ILogger<CacheService> logger, IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _numericComparer = new NumericComparer();
        }

        public bool CacheDirectoryIsAccessible()
        {
            _logger.LogDebug($"Checking if valid Cache directory: {CacheDirectory}");
            var di = new DirectoryInfo(CacheDirectory);
            return di.Exists;
        }

        public async Task<Volume> Ensure(int volumeId)
        {
            if (!CacheDirectoryIsAccessible())
            {
                return null;
            }
            Volume volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            
            foreach (var file in volume.Files)
            {
                var extractPath = GetVolumeCachePath(volumeId, file);
                _archiveService.ExtractArchive(file.FilePath, extractPath);
            }

            return volume;
        }

        public void Cleanup()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            
            if (!CacheDirectoryIsAccessible())
            {
                _logger.LogError($"Cache directory {CacheDirectory} is not accessible or does not exist.");
                return;
            }
            
            DirectoryInfo di = new DirectoryInfo(CacheDirectory);

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
        
        public void CleanupVolumes(int[] volumeIds)
        {
            _logger.LogInformation($"Running Cache cleanup on Volumes");
            
            foreach (var volume in volumeIds)
            {
                var di = new DirectoryInfo(Path.Join(CacheDirectory, volume + ""));
                if (di.Exists)
                {
                    di.Delete(true);    
                }
                
            }
            _logger.LogInformation("Cache directory purged");
        }

        


        public string GetVolumeCachePath(int volumeId, MangaFile file)
        {
            var extractPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), $"../cache/{volumeId}/"));
            if (file.Chapter > 0)
            {
                extractPath = Path.Join(extractPath, file.Chapter + "");
            }
            return extractPath;
        }

        public IEnumerable<MangaFile> GetOrderedChapters(ICollection<MangaFile> files)
        {
            // BUG: This causes a problem because total pages on a volume assumes "specials" to be there
            //return files.OrderBy(f => f.Chapter).Where(f => f.Chapter > 0 || f.Volume.Number != 0);
            return files.OrderBy(f => f.Chapter, new ChapterSortComparer());
        }

        public (string path, MangaFile file) GetCachedPagePath(Volume volume, int page)
        {
            // Calculate what chapter the page belongs to
            var pagesSoFar = 0;
            // Do not allow chapters with 0, as those are specials and break ordering for reading. 
            var orderedChapters = GetOrderedChapters(volume.Files);
            foreach (var mangaFile in orderedChapters)
            {
                if (page + 1 < (mangaFile.NumberOfPages + pagesSoFar))
                {
                    var path = GetVolumeCachePath(volume.Id, mangaFile);
                    var files = _directoryService.GetFiles(path);
                    Array.Sort(files, _numericComparer);
                    
                    return (files.ElementAt(page - pagesSoFar), mangaFile);
                }

                pagesSoFar += mangaFile.NumberOfPages;
            }
            return ("", null);
        }
    }
}