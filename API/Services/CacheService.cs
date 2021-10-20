using System;
using System.Collections.Generic;
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
        private readonly IBookService _bookService;
        private readonly NumericComparer _numericComparer;

        public CacheService(ILogger<CacheService> logger, IUnitOfWork unitOfWork, IArchiveService archiveService,
            IDirectoryService directoryService, IBookService bookService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _archiveService = archiveService;
            _directoryService = directoryService;
            _bookService = bookService;
            _numericComparer = new NumericComparer();
        }

        public void EnsureCacheDirectory()
        {
            if (!DirectoryService.ExistOrCreate(DirectoryService.CacheDirectory))
            {
                _logger.LogError("Cache directory {CacheDirectory} is not accessible or does not exist. Creating...", DirectoryService.CacheDirectory);
            }
        }

        /// <summary>
        /// Returns the full path to the cached epub file. If the file does not exist, will fallback to the original.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <param name="chapter"></param>
        /// <returns></returns>
        public string GetCachedEpubFile(int chapterId, Chapter chapter)
        {
            var extractPath = GetCachePath(chapterId);
            var path = Path.Join(extractPath, Path.GetFileName(chapter.Files.First().FilePath));
            if (!(new FileInfo(path).Exists))
            {
                path = chapter.Files.First().FilePath;
            }
            return path;
        }

        /// <summary>
        /// Caches the files for the given chapter to CacheDirectory
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns>This will always return the Chapter for the chpaterId</returns>
        public async Task<Chapter> Ensure(int chapterId)
        {
            EnsureCacheDirectory();
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
            var extractPath = GetCachePath(chapterId);

            if (!Directory.Exists(extractPath))
            {
                var files = chapter.Files.ToList();
                ExtractChapterFiles(extractPath, files);
            }

            return  chapter;
        }

        /// <summary>
        /// This is an internal method for cache service for extracting chapter files to disk. The code is structured
        /// for cache service, but can be re-used (download bookmarks)
        /// </summary>
        /// <param name="extractPath"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        public void ExtractChapterFiles(string extractPath, IReadOnlyList<MangaFile> files)
        {
            var removeNonImages = true;
            var fileCount = files.Count;
            var extraPath = "";
            var extractDi = new DirectoryInfo(extractPath);

            if (files.Count > 0 && files[0].Format == MangaFormat.Image)
            {
                DirectoryService.ExistOrCreate(extractPath);
                if (files.Count == 1)
                {
                    _directoryService.CopyFileToDirectory(files[0].FilePath, extractPath);
                }
                else
                {
                    DirectoryService.CopyDirectoryToDirectory(Path.GetDirectoryName(files[0].FilePath), extractPath,
                        Parser.Parser.ImageFileExtensions);
                }

                extractDi.Flatten();
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
                else if (file.Format == MangaFormat.Pdf)
                {
                    _bookService.ExtractPdfImages(file.FilePath, Path.Join(extractPath, extraPath));
                }
                else if (file.Format == MangaFormat.Epub)
                {
                    removeNonImages = false;
                    DirectoryService.ExistOrCreate(extractPath);
                    _directoryService.CopyFileToDirectory(files[0].FilePath, extractPath);
                }
            }

            extractDi.Flatten();
            if (removeNonImages)
            {
                extractDi.RemoveNonImages();
            }
        }


        public void Cleanup()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            EnsureCacheDirectory();

            try
            {
                DirectoryService.ClearDirectory(DirectoryService.CacheDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
            }

            _logger.LogInformation("Cache directory purged");
        }

        /// <summary>
        /// Removes the cached files and folders for a set of chapterIds
        /// </summary>
        /// <param name="chapterIds"></param>
        public void CleanupChapters(IEnumerable<int> chapterIds)
        {
            _logger.LogInformation("Running Cache cleanup on Chapters");

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
            return Path.GetFullPath(Path.Join(DirectoryService.CacheDirectory, $"{chapterId}/"));
        }

        public async Task<(string path, MangaFile file)> GetCachedPagePath(Chapter chapter, int page)
        {
            // Calculate what chapter the page belongs to
            var pagesSoFar = 0;
            var chapterFiles = chapter.Files ?? await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapter.Id);
            foreach (var mangaFile in chapterFiles)
            {
                if (page <= (mangaFile.Pages + pagesSoFar))
                {
                    var path = GetCachePath(chapter.Id);
                    var files = DirectoryService.GetFilesWithExtension(path, Parser.Parser.ImageFileExtensions);
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
