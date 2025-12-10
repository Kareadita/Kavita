using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.Middleware;
using API.Services;
using API.Services.Tasks.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for servicing up images stored in Kavita for entities
/// </summary>
[AllowAnonymous]
[SkipDeviceTracking]
public class ImageController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly ILocalizationService _localizationService;
    private readonly IReadingListService _readingListService;
    private readonly ICoverDbService _coverDbService;

    /// <inheritdoc />
    public ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService,
        ILocalizationService localizationService, IReadingListService readingListService,
        ICoverDbService coverDbService)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _localizationService = localizationService;
        _readingListService = readingListService;
        _coverDbService = coverDbService;
    }

    /// <summary>
    /// Returns cover image for Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("chapter-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["chapterId", "apiKey"])]
    public async Task<ActionResult> GetChapterCoverImage(int chapterId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("library-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["libraryId", "apiKey"])]
    public async Task<ActionResult> GetLibraryCoverImage(int libraryId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.LibraryRepository.GetLibraryCoverImageAsync(libraryId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("volume-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["volumeId", "apiKey"])]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["seriesId", "apiKey"])]
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        Response.AddCacheHeader(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Collection
    /// </summary>
    /// <param name="collectionTagId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("collection-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["collectionTagId", "apiKey"])]
    public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();

        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
        {
            var destFile = await GenerateCollectionCoverImage(collectionTagId);
            if (string.IsNullOrEmpty(destFile)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));

            return PhysicalFile(destFile, MimeTypeMap.GetMimeType(_directoryService.FileSystem.Path.GetExtension(destFile)),
                _directoryService.FileSystem.Path.GetFileName(destFile));
        }
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("readinglist-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["readingListId", "apiKey"])]
    public async Task<ActionResult> GetReadingListCoverImage(int readingListId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();

        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ReadingListRepository.GetCoverImageAsync(readingListId));

        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
        {
            var destFile = await _readingListService.GenerateReadingListCoverImage(readingListId);
            if (string.IsNullOrEmpty(destFile)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
            return PhysicalFile(destFile, MimeTypeMap.GetMimeType(_directoryService.FileSystem.Path.GetExtension(destFile)), _directoryService.FileSystem.Path.GetFileName(destFile));
        }

        var format = _directoryService.FileSystem.Path.GetExtension(path);
        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    private async Task<string> GenerateCollectionCoverImage(int collectionId)
    {
        var covers = await _unitOfWork.CollectionTagRepository.GetRandomCoverImagesAsync(collectionId);
        var destFile = _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory,
            ImageService.GetCollectionTagFormat(collectionId));
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        destFile += settings.EncodeMediaAs.GetExtension();

        if (_directoryService.FileSystem.File.Exists(destFile)) return destFile;
        ImageService.CreateMergedImage(
            covers.Select(c => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, c)).ToList(),
            settings.CoverImageSize,
            destFile);
        // TODO: Refactor this so that collections have a dedicated cover image so we can calculate primary/secondary colors
        return !_directoryService.FileSystem.File.Exists(destFile) ? string.Empty : destFile;
    }

    /// <summary>
    /// Returns image for a given bookmark page
    /// </summary>
    /// <remarks>This request is served unauthenticated, but user must be passed via api key to validate</remarks>
    /// <param name="chapterId"></param>
    /// <param name="pageNum">Starts at 0</param>
    /// <param name="apiKey">API Key for user. Needed to authenticate request</param>
    /// <param name="imageOffset">Only applicable for Epubs - handles multiple images on one page</param>
    /// <returns></returns>
    [HttpGet("bookmark")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["chapterId", "pageNum", "apiKey", "imageOffset"])]
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, string apiKey, int imageOffset = 0)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, imageOffset, userId);
        if (bookmark == null) return BadRequest(await _localizationService.Translate(userId, "bookmark-doesnt-exist"));

        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var file = new FileInfo(Path.Join(bookmarkDirectory, bookmark.FileName));
        var format = Path.GetExtension(file.FullName);

        return PhysicalFile(file.FullName, MimeTypeMap.GetMimeType(format), Path.GetFileName(file.FullName));
    }

    /// <summary>
    /// Returns the image associated with a web-link
    /// </summary>
    /// <param name="url"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("web-link")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month, VaryByQueryKeys = ["url", "apiKey"])]
    public async Task<ActionResult> GetWebLinkImage(string url, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        if (string.IsNullOrEmpty(url)) return BadRequest(await _localizationService.Translate(userId, "must-be-defined", "Url"));

        var encodeFormat = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        // Check if the domain exists
        var domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.FaviconDirectory, ImageService.GetWebLinkFormat(url, encodeFormat));
        if (!_directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.FaviconDirectory,
                    await _coverDbService.DownloadFaviconAsync(url, encodeFormat));
            }
            catch (Exception)
            {
                return BadRequest(await _localizationService.Translate(userId, "generic-favicon"));
            }
        }

        var file = new FileInfo(domainFilePath);
        var format = Path.GetExtension(file.FullName);

        return PhysicalFile(file.FullName, MimeTypeMap.GetMimeType(format), Path.GetFileName(file.FullName));
    }


    /// <summary>
    /// Returns the image associated with a publisher
    /// </summary>
    /// <param name="publisherName"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("publisher")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month, VaryByQueryKeys = ["publisherName", "apiKey"])]
    public async Task<ActionResult> GetPublisherImage(string publisherName, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        if (string.IsNullOrEmpty(publisherName)) return BadRequest(await _localizationService.Translate(userId, "must-be-defined", "publisherName"));
        if (publisherName.Contains("..")) return BadRequest();

        var encodeFormat = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        // Check if the domain exists
        var domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.PublisherDirectory, ImageService.GetPublisherFormat(publisherName, encodeFormat));
        if (!_directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = _directoryService.FileSystem.Path.Join(_directoryService.PublisherDirectory,
                    await _coverDbService.DownloadPublisherImageAsync(publisherName, encodeFormat));
            }
            catch (Exception)
            {
                return BadRequest(await _localizationService.Translate(userId, "generic-favicon"));
            }
        }

        var file = new FileInfo(domainFilePath);
        var format = Path.GetExtension(file.FullName);

        return PhysicalFile(file.FullName, MimeTypeMap.GetMimeType(format), Path.GetFileName(file.FullName));
    }

    /// <summary>
    /// Returns cover image for Person
    /// </summary>
    /// <param name="personId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("person-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["personId", "apiKey"])]
    public async Task<ActionResult> GetPersonCoverImage(int personId, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.UserRepository.GetPersonCoverImageAsync(personId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for User
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("user-cover")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["userId", "apiKey"])]
    public async Task<ActionResult> GetUserCoverImage(int userId, string apiKey)
    {
        var authedUser = await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (authedUser == 0 || userId == 0) return BadRequest();

        var filename = await _unitOfWork.UserRepository.GetCoverImageAsync(userId, authedUser);
        var path = Path.Join(_directoryService.CoverImageDirectory, filename);
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-cover-image"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns a temp coverupload image
    /// </summary>
    /// <remarks>Requires Admin Role to perform upload</remarks>
    /// <param name="filename">Filename of file. This is used with upload/upload-by-url</param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("cover-upload")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Images, VaryByQueryKeys = ["filename", "apiKey"])]
    public async Task<ActionResult> GetCoverUploadImage(string filename, string apiKey)
    {
        if (!UserContext.IsAuthenticated) return Unauthorized();
        if (filename.Contains("..")) return BadRequest(await _localizationService.Translate(UserId, "invalid-filename"));

        var roles = await _unitOfWork.UserRepository.GetRolesByAuthKey(apiKey);
        if (!roles.Contains(PolicyConstants.AdminRole))
        {
            return Forbid();
        }

        var path = Path.Join(_directoryService.TempDirectory, filename);
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
            return BadRequest(await _localizationService.Translate(UserId, "file-doesnt-exist"));
        var format = _directoryService.FileSystem.Path.GetExtension(path);

        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), _directoryService.FileSystem.Path.GetFileName(path));
    }
}
