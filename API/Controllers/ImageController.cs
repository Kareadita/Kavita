using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

/// <summary>
/// Responsible for servicing up images stored in Kavita for entities
/// </summary>
[AllowAnonymous]
public class ImageController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;

    /// <inheritdoc />
    public ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService, IImageService imageService)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _imageService = imageService;
    }

    /// <summary>
    /// Returns cover image for Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"chapterId", "apiKey"})]
    public async Task<ActionResult> GetChapterCoverImage(int chapterId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("library-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"libraryId", "apiKey"})]
    public async Task<ActionResult> GetLibraryCoverImage(int libraryId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.LibraryRepository.GetLibraryCoverImageAsync(libraryId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"volumeId", "apiKey"})]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"seriesId", "apiKey"})]
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        Response.AddCacheHeader(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Collection Tag
    /// </summary>
    /// <param name="collectionTagId"></param>
    /// <returns></returns>
    [HttpGet("collection-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"collectionTagId", "apiKey"})]
    public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("readinglist-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"readingListId", "apiKey"})]
    public async Task<ActionResult> GetReadingListCoverImage(int readingListId, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ReadingListRepository.GetCoverImageAsync(readingListId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns image for a given bookmark page
    /// </summary>
    /// <remarks>This request is served unauthenticated, but user must be passed via api key to validate</remarks>
    /// <param name="chapterId"></param>
    /// <param name="pageNum">Starts at 0</param>
    /// <param name="apiKey">API Key for user. Needed to authenticate request</param>
    /// <returns></returns>
    [HttpGet("bookmark")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"chapterId", "pageNum", "apiKey"})]
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, userId);
        if (bookmark == null) return BadRequest("Bookmark does not exist");

        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var file = new FileInfo(Path.Join(bookmarkDirectory, bookmark.FileName));
        var format = Path.GetExtension(file.FullName);

        return PhysicalFile(file.FullName, MimeTypeMap.GetMimeType(format), Path.GetFileName(file.FullName));
    }

    /// <summary>
    /// Returns the image associated with a web-link
    /// </summary>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("web-link")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month, VaryByQueryKeys = new []{"url", "apiKey"})]
    public async Task<ActionResult> GetWebLinkImage(string url, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        if (string.IsNullOrEmpty(url)) return BadRequest("Url cannot be null");
        var encodeFormat = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        // Check if the domain exists
        var domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.FaviconDirectory, ImageService.GetWebLinkFormat(url, encodeFormat));
        if (!_directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.FaviconDirectory,
                    await _imageService.DownloadFaviconAsync(url, encodeFormat));
            }
            catch (Exception)
            {
                return BadRequest("There was an issue fetching favicon for domain");
            }
        }

        var file = new FileInfo(domainFilePath);
        var format = Path.GetExtension(file.FullName);

        return PhysicalFile(file.FullName, MimeTypeMap.GetMimeType(format), Path.GetFileName(file.FullName));
    }

    /// <summary>
    /// Returns a temp coverupload image
    /// </summary>
    /// <param name="filename">Filename of file. This is used with upload/upload-by-url</param>
    /// <returns></returns>
    [Authorize(Policy="RequireAdminRole")]
    [HttpGet("cover-upload")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = new []{"filename", "apiKey"})]
    public async Task<ActionResult> GetCoverUploadImage(string filename, string apiKey)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        if (filename.Contains("..")) return BadRequest("Invalid Filename");

        var path = Path.Join(_directoryService.TempDirectory, filename);
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"File does not exist");
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }
}
