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
        private readonly ILogger<CacheService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;
        private readonly NumericComparer _numericComparer;
        public static readonly string CacheDirectory = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "cache/"));

        public CacheService(ILogger<CacheService> logger, IUnitOfWork unitOfWork, IArchiveService archiveService, IDirectoryService directoryService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _numericComparer = new NumericComparer();
        }

        public void EnsureCacheDirectory()
        {
            _logger.LogDebug($"Checking if valid Cache directory: {CacheDirectory}");
            var di = new DirectoryInfo(CacheDirectory);
            if (!di.Exists)
            {
                _logger.LogError($"Cache directory {CacheDirectory} is not accessible or does not exist. Creating...");
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        public async Task<Chapter> Ensure(int chapterId)
        {
            EnsureCacheDirectory();
            Chapter chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(chapterId);
            
            foreach (var file in chapter.Files)
            {
                var extractPath = GetCachePath(chapterId);
                _archiveService.ExtractArchive(file.FilePath, extractPath);
            }

            return chapter;
        }

        public void Cleanup()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            EnsureCacheDirectory();

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
        
        public void CleanupChapters(int[] chapterIds)
        {
            _logger.LogInformation($"Running Cache cleanup on Volumes");
            
            foreach (var chapter in chapterIds)
            {
                var di = new DirectoryInfo(GetCachePath(chapter));
                if (di.Exists)
                {
                    di.Delete(true);    
                }
                
            }
            _logger.LogInformation("Cache directory purged");
        }


        /// <summary>
        /// Returns the cache path for a given Chapter. Should be cacheDirectory/{chapterId}/
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        private string GetCachePath(int chapterId)
        {
            return Path.GetFullPath(Path.Join(CacheDirectory, $"{chapterId}/"));
        }

        public async Task<(string path, MangaFile file)> GetCachedPagePath(Chapter chapter, int page)
        {
            // Calculate what chapter the page belongs to
            var pagesSoFar = 0;
            var chapterFiles = chapter.Files ?? await _unitOfWork.VolumeRepository.GetFilesForChapter(chapter.Id);
            foreach (var mangaFile in chapterFiles)
            {
                if (page <= (mangaFile.NumberOfPages + pagesSoFar))
                {
                    var path = GetCachePath(chapter.Id);
                    var files = _directoryService.GetFiles(path, Parser.Parser.ImageFileExtensions); 
                    Array.Sort(files, _numericComparer);
                    
                    // Since array is 0 based, we need to keep that in account (only affects last image)
                    if (page - 1 == files.Length)
                    {
                        return (files.ElementAt(page - 1 - pagesSoFar), mangaFile);
                    }
                    
                    return (files.ElementAt(page - pagesSoFar), mangaFile);
                }
            
                pagesSoFar += mangaFile.NumberOfPages;
            }

            return ("", null);
        }
    }
}