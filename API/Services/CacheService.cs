using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface ICacheService
{
    /// <summary>
    /// Ensures the cache is created for the given chapter and if not, will create it. Should be called before any other
    /// cache operations (except cleanup).
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns>Chapter for the passed chapterId. Side-effect from ensuring cache.</returns>
    Task<Chapter> Ensure(int chapterId);
    /// <summary>
    /// Clears cache directory of all volumes. This can be invoked from deleting a library or a series.
    /// </summary>
    /// <param name="chapterIds">Volumes that belong to that library. Assume the library might have been deleted before this invocation.</param>
    void CleanupChapters(IEnumerable<int> chapterIds);
    void CleanupBookmarks(IEnumerable<int> seriesIds);
    string GetCachedPagePath(Chapter chapter, int page);
    string GetCachedBookmarkPagePath(int seriesId, int page);
    string GetCachedFile(Chapter chapter);
    public void ExtractChapterFiles(string extractPath, IReadOnlyList<MangaFile> files);
    Task<int> CacheBookmarkForSeries(int userId, int seriesId);
    void CleanupBookmarkCache(int seriesId);
}
public class CacheService : ICacheService
{
    private readonly ILogger<CacheService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingItemService _readingItemService;
    private readonly IBookmarkService _bookmarkService;

    public CacheService(ILogger<CacheService> logger, IUnitOfWork unitOfWork,
        IDirectoryService directoryService, IReadingItemService readingItemService,
        IBookmarkService bookmarkService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _readingItemService = readingItemService;
        _bookmarkService = bookmarkService;
    }

    public string GetCachedBookmarkPagePath(int seriesId, int page)
    {
        // Calculate what chapter the page belongs to
        var path = GetBookmarkCachePath(seriesId);
        var files = _directoryService.GetFilesWithExtension(path, Tasks.Scanner.Parser.Parser.ImageFileExtensions);
        files = files
            .AsEnumerable()
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (files.Length == 0)
        {
            return string.Empty;
        }

        // Since array is 0 based, we need to keep that in account (only affects last image)
        return page == files.Length ? files.ElementAt(page - 1) : files.ElementAt(page);
    }

    /// <summary>
    /// Returns the full path to the cached file. If the file does not exist, will fallback to the original.
    /// </summary>
    /// <param name="chapter"></param>
    /// <returns></returns>
    public string GetCachedFile(Chapter chapter)
    {
        var extractPath = GetCachePath(chapter.Id);
        var path = Path.Join(extractPath, _directoryService.FileSystem.Path.GetFileName(chapter.Files.First().FilePath));
        if (!(_directoryService.FileSystem.FileInfo.FromFileName(path).Exists))
        {
            path = chapter.Files.First().FilePath;
        }
        return path;
    }


    /// <summary>
    /// Caches the files for the given chapter to CacheDirectory
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns>This will always return the Chapter for the chapterId</returns>
    public async Task<Chapter> Ensure(int chapterId)
    {
        _directoryService.ExistOrCreate(_directoryService.CacheDirectory);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        var extractPath = GetCachePath(chapterId);

        if (_directoryService.Exists(extractPath)) return chapter;
        var files = chapter.Files.ToList();
        ExtractChapterFiles(extractPath, files);

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
        var extractDi = _directoryService.FileSystem.DirectoryInfo.FromDirectoryName(extractPath);

        if (files.Count > 0 && files[0].Format == MangaFormat.Image)
        {
            _readingItemService.Extract(files[0].FilePath, extractPath, MangaFormat.Image, files.Count);
            _directoryService.Flatten(extractDi.FullName);
        }

        foreach (var file in files)
        {
            if (fileCount > 1)
            {
                extraPath = file.Id + string.Empty;
            }

            switch (file.Format)
            {
                case MangaFormat.Archive:
                    _readingItemService.Extract(file.FilePath, Path.Join(extractPath, extraPath), file.Format);
                    break;
                case MangaFormat.Epub:
                case MangaFormat.Pdf:
                {
                    removeNonImages = false;
                    if (!_directoryService.FileSystem.File.Exists(files[0].FilePath))
                    {
                        _logger.LogError("{File} does not exist on disk", files[0].FilePath);
                        throw new KavitaException($"{files[0].FilePath} does not exist on disk");
                    }

                    _directoryService.ExistOrCreate(extractPath);
                    _directoryService.CopyFileToDirectory(files[0].FilePath, extractPath);
                    break;
                }
            }
        }

        _directoryService.Flatten(extractDi.FullName);
        if (removeNonImages)
        {
            _directoryService.RemoveNonImages(extractDi.FullName);
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
            _directoryService.ClearAndDeleteDirectory(GetCachePath(chapter));
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
            _directoryService.ClearAndDeleteDirectory(GetBookmarkCachePath(series));
        }
    }


    /// <summary>
    /// Returns the cache path for a given Chapter. Should be cacheDirectory/{chapterId}/
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    private string GetCachePath(int chapterId)
    {
        return _directoryService.FileSystem.Path.GetFullPath(_directoryService.FileSystem.Path.Join(_directoryService.CacheDirectory, $"{chapterId}/"));
    }

    private string GetBookmarkCachePath(int seriesId)
    {
        return _directoryService.FileSystem.Path.GetFullPath(_directoryService.FileSystem.Path.Join(_directoryService.CacheDirectory, $"{seriesId}_bookmarks/"));
    }

    /// <summary>
    /// Returns the absolute path of a cached page.
    /// </summary>
    /// <param name="chapter">Chapter entity with Files populated.</param>
    /// <param name="page">Page number to look for</param>
    /// <returns>Page filepath or empty if no files found.</returns>
    public string GetCachedPagePath(Chapter chapter, int page)
    {
        // Calculate what chapter the page belongs to
        var path = GetCachePath(chapter.Id);
        // TODO: We can optimize this by extracting and renaming, so we don't need to scan for the files and can do a direct access
        var files = _directoryService.GetFilesWithExtension(path, Tasks.Scanner.Parser.Parser.ImageFileExtensions)
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (files.Length == 0)
        {
            return string.Empty;
        }

        if (page > files.Length) page = files.Length;

        // Since array is 0 based, we need to keep that in account (only affects last image)
        return page == files.Length ? files.ElementAt(page - 1) : files.ElementAt(page);
    }

    public async Task<int> CacheBookmarkForSeries(int userId, int seriesId)
    {
        var destDirectory = _directoryService.FileSystem.Path.Join(_directoryService.CacheDirectory, seriesId + "_bookmarks");
        if (_directoryService.Exists(destDirectory)) return _directoryService.GetFiles(destDirectory).Count();

        var bookmarkDtos = await _unitOfWork.UserRepository.GetBookmarkDtosForSeries(userId, seriesId);
        var files = (await _bookmarkService.GetBookmarkFilesById(bookmarkDtos.Select(b => b.Id))).ToList();
        _directoryService.CopyFilesToDirectory(files, destDirectory,
            Enumerable.Range(1, files.Count).Select(i => i + string.Empty).ToList());
        return files.Count;
    }

    /// <summary>
    /// Clears a cached bookmarks for a series id folder
    /// </summary>
    /// <param name="seriesId"></param>
    public void CleanupBookmarkCache(int seriesId)
    {
        var destDirectory = _directoryService.FileSystem.Path.Join(_directoryService.CacheDirectory, seriesId + "_bookmarks");
        if (!_directoryService.Exists(destDirectory)) return;

        _directoryService.ClearAndDeleteDirectory(destDirectory);
    }
}
