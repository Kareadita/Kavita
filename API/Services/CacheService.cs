using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        private readonly ILogger<CacheService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly NumericComparer _numericComparer;
        public static readonly string CacheDirectory = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../cache/"));

        public CacheService(IDirectoryService directoryService, ILogger<CacheService> logger, IUnitOfWork unitOfWork)
        {
            _directoryService = directoryService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _numericComparer = new NumericComparer();
        }

        private bool CacheDirectoryIsAccessible()
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
                ExtractArchive(file.FilePath, extractPath);
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

        /// <summary>
        /// Extracts an archive to a temp cache directory. Returns path to new directory. If temp cache directory already exists,
        /// will return that without performing an extraction. Returns empty string if there are any invalidations which would
        /// prevent operations to perform correctly (missing archivePath file, empty archive, etc).
        /// </summary>
        /// <param name="archivePath">A valid file to an archive file.</param>
        /// <param name="extractPath">Path to extract to</param>
        /// <returns></returns>
        private void ExtractArchive(string archivePath, string extractPath)
        {
            if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath))
            {
                _logger.LogError($"Archive {archivePath} could not be found.");
            }

            if (Directory.Exists(extractPath))
            {
                _logger.LogDebug($"Archive {archivePath} has already been extracted. Returning existing folder.");
            }
           
            Stopwatch sw = Stopwatch.StartNew();
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            // TODO: Throw error if we couldn't extract
            var needsFlattening = archive.Entries.Count > 0 && !Path.HasExtension(archive.Entries.ElementAt(0).FullName);
            if (!archive.HasFiles() && !needsFlattening) return;
            
            archive.ExtractToDirectory(extractPath);
            _logger.LogDebug($"[OLD] Extracted archive to {extractPath} in {sw.ElapsedMilliseconds} milliseconds.");

            if (needsFlattening)
            {
                sw = Stopwatch.StartNew();
                _logger.LogInformation("Extracted archive is nested in root folder, flattening...");
                new DirectoryInfo(extractPath).Flatten();
                _logger.LogInformation($"[OLD] Flattened in {sw.ElapsedMilliseconds} milliseconds");
            }
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
                    var files = DirectoryService.GetFiles(path);
                    Array.Sort(files, _numericComparer);
                    
                    return files.ElementAt(page - pagesSoFar);
                }

                pagesSoFar += mangaFile.NumberOfPages;
            }
            return "";
        }
    }
}