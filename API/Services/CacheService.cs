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
                var extractPath = GetCachePath(chapterId, file);
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
        
        public void CleanupVolumes(int[] volumeIds)
        {
            // TODO: Fix this code to work with chapters
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
        /// Returns the cache path for a given Chapter. Should be cacheDirectory/{chapterId}/
        /// </summary>
        /// <param name="chapterId"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public string GetCachePath(int chapterId, MangaFile file)
        {
            var extractPath = Path.GetFullPath(Path.Join(CacheDirectory, $"{chapterId}/"));
            // if (file.Chapter != null)
            // {
            //     extractPath = Path.Join(extractPath, chapterId + "");
            // }
            return extractPath;
        }

        public IEnumerable<MangaFile> GetOrderedChapters(ICollection<MangaFile> files)
        {
            // BUG: This causes a problem because total pages on a volume assumes "specials" to be there
            //return files.OrderBy(f => f.Chapter).Where(f => f.Chapter > 0 || f.Volume.Number != 0);
            return files;
            //return files.OrderBy(f => f.Chapter, new ChapterSortComparer());
        }

        public async Task<(string path, MangaFile file)> GetCachedPagePath(Chapter chapter, int page)
        {
            // Calculate what chapter the page belongs to
            var pagesSoFar = 0;
            var chapterFiles = chapter.Files ?? await _unitOfWork.VolumeRepository.GetFilesForChapter(chapter.Id);
            foreach (var mangaFile in chapterFiles)
            {
                if (page + 1 < (mangaFile.NumberOfPages + pagesSoFar))
                {
                    var path = GetCachePath(chapter.Id, mangaFile);
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