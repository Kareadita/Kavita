using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NetVips;

namespace Kavita.Services;

public class CacheService(
    ILogger<CacheService> logger,
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    IReadingItemService readingItemService,
    IBookmarkService bookmarkService,
    ILocalizationService localizationService)
    : ICacheService
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ExtractLocks = new();

    public IEnumerable<string> GetCachedPages(int chapterId)
    {
        var path = GetCachePath(chapterId);
        return directoryService.GetFilesWithExtension(path, Parser.ImageFileExtensions)
            .OrderByNatural(Path.GetFileNameWithoutExtension);
    }

    /// <summary>
    /// For a given path, scan all files (in reading order) and generate File Dimensions for it. Path must exist
    /// </summary>
    /// <param name="cachePath"></param>
    /// <returns></returns>
    public IEnumerable<FileDimensionDto> GetCachedFileDimensions(string cachePath)
    {
        var files = directoryService.GetFilesWithExtension(cachePath, Parser.ImageFileExtensions)
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (files.Length == 0)
        {
            return ArraySegment<FileDimensionDto>.Empty;
        }

        var dimensions = new List<FileDimensionDto>();
        var originalCacheSize = Cache.MaxFiles;
        try
        {
            Cache.MaxFiles = 0;
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                using var image = Image.NewFromFile(file, memory: false, access: Enums.Access.SequentialUnbuffered);
                dimensions.Add(new FileDimensionDto()
                {
                    PageNumber = i,
                    Height = image.Height,
                    Width = image.Width,
                    IsWide = image.Width > image.Height,
                    FileName = file.Replace(cachePath, string.Empty)
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error calculating image dimensions for {CachePath}", cachePath);
        }
        finally
        {
            Cache.MaxFiles = originalCacheSize;
        }

        return dimensions;
    }

    public string GetCachedBookmarkPagePath(int seriesId, int page)
    {
        // Calculate what chapter the page belongs to
        var path = GetBookmarkCachePath(seriesId);
        var files = directoryService.GetFilesWithExtension(path, Parser.ImageFileExtensions);
        files = files
            .AsEnumerable()
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (files.Length == 0)
        {
            return string.Empty;
        }

        // Since array is 0 based, we need to keep that in account (only affects last image)
        return page == files.Length ? files[page - 1] : files[page];
    }

    /// <summary>
    /// Returns the full path to the cached file. If the file does not exist, will fallback to the original.
    /// </summary>
    /// <param name="chapter"></param>
    /// <returns></returns>
    public string GetCachedFile(Chapter chapter)
    {
        var extractPath = GetCachePath(chapter.Id);
        var path = Path.Join(extractPath, directoryService.FileSystem.Path.GetFileName(chapter.Files.First().FilePath));
        if (!(directoryService.FileSystem.FileInfo.New(path).Exists))
        {
            path = chapter.Files.First().FilePath;
        }
        return path;
    }

    public string GetCachedFile(int chapterId, string firstFilePath)
    {
        var extractPath = GetCachePath(chapterId);
        var path = Path.Join(extractPath, directoryService.FileSystem.Path.GetFileName(firstFilePath));
        if (!(directoryService.FileSystem.FileInfo.New(path).Exists))
        {
            path = firstFilePath;
        }
        return path;
    }


    /// <summary>
    /// Caches the files for the given chapter to CacheDirectory
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="extractPdfToImages">Defaults to false. Extract pdf file into images rather than copying just the pdf file</param>
    /// <param name="ct"></param>
    /// <returns>This will always return the Chapter for the chapterId</returns>
    public async Task<Chapter?> Ensure(int chapterId, bool extractPdfToImages = false, CancellationToken ct = default)
    {
        directoryService.ExistOrCreate(directoryService.CacheDirectory);
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ct: ct);
        var extractPath = GetCachePath(chapterId);

        var extractLock = ExtractLocks.GetOrAdd(chapterId, id => new SemaphoreSlim(1,1));

        await extractLock.WaitAsync(ct);

        try {
            if (directoryService.Exists(extractPath))
            {
                if (extractPdfToImages)
                {
                    var pdfImages = directoryService.GetFiles(extractPath, Parser.ImageFileExtensions);
                    if (pdfImages.Any())
                    {
                        return chapter;
                    }
                }
                else
                {
                    // Do an explicit check for files since rarely a "permission denied" error on deleting
                    // the file can occur, thus leaving an empty folder and we would never re-cache the files.
                    if (directoryService.GetFiles(extractPath).Any())
                    {
                        return chapter;
                    }

                    // Delete the extractPath as ExtractArchive will return if the directory already exists
                    directoryService.ClearAndDeleteDirectory(extractPath);
                }
            }

            var files = chapter?.Files.ToList();
            await ExtractChapterFiles(extractPath, files, extractPdfToImages);
        } finally {
            extractLock.Release();
        }

        return chapter;
    }

    /// <summary>
    /// This is an internal method for cache service for extracting chapter files to disk. The code is structured
    /// for cache service, but can be re-used (download bookmarks)
    /// </summary>
    /// <param name="extractPath"></param>
    /// <param name="files"></param>
    /// <param name="extractPdfImages">Defaults to false, if true, will extract the images from the PDF renderer and not move the pdf file</param>
    /// <returns></returns>
    public async Task ExtractChapterFiles(string extractPath, IReadOnlyList<MangaFile>? files, bool extractPdfImages = false)
    {
        if (files == null || files.Count == 0) return;
        var removeNonImages = true;
        var fileCount = files.Count;
        var extraPath = string.Empty;
        var extractDi = directoryService.FileSystem.DirectoryInfo.New(extractPath);

        if (files[0].Format == MangaFormat.Image)
        {
            // Check if all the files are Images. If so, do a directory copy, else do the normal copy
            if (files.All(f => f.Format == MangaFormat.Image))
            {
                directoryService.ExistOrCreate(extractPath);
                directoryService.CopyFilesToDirectory(files.Select(f => f.FilePath), extractPath);
            }
            else
            {
                foreach (var file in files)
                {
                    if (fileCount > 1)
                    {
                        extraPath = file.Id + string.Empty;
                    }
                    readingItemService.Extract(file.FilePath, Path.Join(extractPath, extraPath), MangaFormat.Image, files.Count);
                }
                directoryService.Flatten(extractDi.FullName);
            }

        }

        foreach (var file in files)
        {
            if (fileCount > 1)
            {
                extraPath = file.Id + string.Empty;
            }

            if (!directoryService.FileSystem.Path.Exists(file.FilePath))
            {
                logger.LogError("{File} does not exist on disk", file.FilePath);
                throw new KavitaException(await localizationService.TranslateAsync("file-doesnt-exist"));
            }

            switch (file.Format)
            {
                case MangaFormat.Archive:
                    readingItemService.Extract(file.FilePath, Path.Join(extractPath, extraPath), file.Format);
                    break;
                case MangaFormat.Epub:
                case MangaFormat.Pdf:
                {
                    if (extractPdfImages)
                    {
                        readingItemService.Extract(file.FilePath, Path.Join(extractPath, extraPath), file.Format);
                        break;
                    }
                    removeNonImages = false;

                    directoryService.ExistOrCreate(extractPath);
                    directoryService.CopyFileToDirectory(files[0].FilePath, extractPath);
                    break;
                }
            }
        }

        directoryService.Flatten(extractDi.FullName);
        if (removeNonImages)
        {
            directoryService.RemoveNonImages(extractDi.FullName);
        }
    }

    /// <summary>
    /// Removes the cached files and folders for a set of chapterIds
    /// </summary>
    /// <param name="chapterIds"></param>
    public void CleanupChapters(IEnumerable<int> chapterIds)
    {
        foreach (var chapter in chapterIds)
        {
            directoryService.ClearAndDeleteDirectory(GetCachePath(chapter));
        }
    }

    /// <summary>
    /// Removes the cached files and folders for a set of chapterIds
    /// </summary>
    /// <param name="seriesIds"></param>
    public void CleanupBookmarks(IEnumerable<int> seriesIds)
    {
        foreach (var series in seriesIds)
        {
            directoryService.ClearAndDeleteDirectory(GetBookmarkCachePath(series));
        }
    }


    /// <summary>
    /// Returns the cache path for a given Chapter. Should be cacheDirectory/{chapterId}/
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    public string GetCachePath(int chapterId)
    {
        return directoryService.FileSystem.Path.GetFullPath(directoryService.FileSystem.Path.Join(directoryService.CacheDirectory, $"{chapterId}/"));
    }

    /// <summary>
    /// Returns the cache path for a given series' bookmarks. Should be cacheDirectory/{seriesId_bookmarks}/
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public string GetBookmarkCachePath(int seriesId)
    {
        return directoryService.FileSystem.Path.GetFullPath(directoryService.FileSystem.Path.Join(directoryService.CacheDirectory, $"{seriesId}_bookmarks/"));
    }

    /// <summary>
    /// Returns the absolute path of a cached page.
    /// </summary>
    /// <param name="chapterId">Chapter id with Files populated.</param>
    /// <param name="page">Page number to look for</param>
    /// <returns>Page filepath or empty if no files found.</returns>
    public string GetCachedPagePath(int chapterId, int page)
    {
        // Calculate what chapter the page belongs to
        var path = GetCachePath(chapterId);
        // NOTE: We can optimize this by extracting and renaming, so we don't need to scan for the files and can do a direct access
        var files = directoryService.GetFilesWithExtension(path, Parser.ImageFileExtensions);

        return GetPageFromFiles(files, page);
    }

    public async Task<int> CacheBookmarkForSeries(int userId, int seriesId, CancellationToken ct = default)
    {
        var destDirectory = directoryService.FileSystem.Path.Join(directoryService.CacheDirectory, seriesId + "_bookmarks");
        if (directoryService.Exists(destDirectory)) return directoryService.GetFiles(destDirectory).Count();

        var bookmarkDtos = await unitOfWork.UserRepository.GetBookmarkDtosForSeries(userId, seriesId, ct);

        var files = (await bookmarkService.GetBookmarkFilesById(bookmarkDtos.Select(b => b.Id), ct)).ToList();
        directoryService.CopyFilesToDirectory(files, destDirectory,
            Enumerable.Range(1, files.Count).Select(i => i + string.Empty).ToList());

        return files.Count;
    }

    /// <summary>
    /// Clears a cached bookmarks for a series id folder
    /// </summary>
    /// <param name="seriesId"></param>
    public void CleanupBookmarkCache(int seriesId)
    {
        var destDirectory = directoryService.FileSystem.Path.Join(directoryService.CacheDirectory, seriesId + "_bookmarks");
        if (!directoryService.Exists(destDirectory)) return;

        directoryService.ClearAndDeleteDirectory(destDirectory);
    }

    /// <summary>
    /// Returns either the file or an empty string
    /// </summary>
    /// <param name="files"></param>
    /// <param name="pageNum"></param>
    /// <returns></returns>
    public static string GetPageFromFiles(string[] files, int pageNum)
    {
        files = files
            .AsEnumerable()
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (files.Length == 0)
        {
            return string.Empty;
        }

        if (pageNum < 0)
        {
            pageNum = 0;
        }

        // Since array is 0 based, we need to keep that in account (only affects last image)
        return pageNum >= files.Length ? files[Math.Min(pageNum - 1, files.Length - 1)] : files[pageNum];
    }


}
