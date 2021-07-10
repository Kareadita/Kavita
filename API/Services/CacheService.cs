using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
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

        public CacheService(ILogger<CacheService> logger, IUnitOfWork unitOfWork, IArchiveService archiveService,
            IDirectoryService directoryService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _numericComparer = new NumericComparer();
        }

        public void EnsureCacheDirectory()
        {
            if (!DirectoryService.ExistOrCreate(CacheDirectory))
            {
                _logger.LogError("Cache directory {CacheDirectory} is not accessible or does not exist. Creating...", CacheDirectory);
            }
        }

        public async Task<Chapter> Ensure(int chapterId)
        {
            EnsureCacheDirectory();
            var chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(chapterId);
            var files = chapter.Files.ToList();
            var fileCount = files.Count;
            var extractPath = GetCachePath(chapterId);
            var extraPath = "";

            if (Directory.Exists(extractPath))
            {
              return chapter;
            }

            var extractDi = new DirectoryInfo(extractPath);

            if (files.Count > 0 && files[0].Format == MangaFormat.Image)
            {
              DirectoryService.ExistOrCreate(extractPath);
              _directoryService.CopyDirectoryToDirectory(Path.GetDirectoryName(files[0].FilePath), extractPath);
              extractDi.Flatten();
              return chapter;
            }

            foreach (var file in files)
            {
              if (fileCount > 1)
              {
                extraPath = file.Id + string.Empty;
              }

              if (file.Format == MangaFormat.Archive)
              {
                _archiveService.ExtractArchive(file.FilePath, Path.Join(extractPath, extraPath));
              }
            }

            extractDi.Flatten();
            extractDi.RemoveNonImages();

            return chapter;
        }


        public void Cleanup()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            EnsureCacheDirectory();

            try
            {
                DirectoryService.ClearDirectory(CacheDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
            }

            _logger.LogInformation("Cache directory purged");
        }

        public void CleanupChapters(int[] chapterIds)
        {
            _logger.LogInformation("Running Cache cleanup on Volumes");

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
                if (page <= (mangaFile.Pages + pagesSoFar))
                {
                    var path = GetCachePath(chapter.Id);
                    var files = _directoryService.GetFilesWithExtension(path, Parser.Parser.ImageFileExtensions);
                    Array.Sort(files, _numericComparer);

                    if (files.Length == 0)
                    {
                        return (files.ElementAt(0), mangaFile);
                    }

                    // Since array is 0 based, we need to keep that in account (only affects last image)
                    if (page == files.Length)
                    {
                        return (files.ElementAt(page - 1 - pagesSoFar), mangaFile);
                    }

                    if (mangaFile.Format == MangaFormat.Image && mangaFile.Pages == 1)
                    {
                      // Each file is one page, meaning we should just get element at page
                      return (files.ElementAt(page), mangaFile);
                    }

                    return (files.ElementAt(page - pagesSoFar), mangaFile);
                }

                pagesSoFar += mangaFile.Pages;
            }

            return (string.Empty, null);
        }
    }
}
