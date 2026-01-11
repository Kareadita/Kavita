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
    public async Task<ActionResult> GetChapterCoverImage(int chapterId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("library-cover")]
    public async Task<ActionResult> GetLibraryCoverImage(int libraryId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.LibraryRepository.GetLibraryCoverImageAsync(libraryId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("volume-cover")]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Collection
    /// </summary>
    /// <param name="collectionTagId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("collection-cover")]
    public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
        {
            // TODO: Streamline this like ReadingList does
            path = await GenerateCollectionCoverImage(collectionTagId);
        }

        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("readinglist-cover")]
    public async Task<ActionResult> GetReadingListCoverImage(int readingListId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ReadingListRepository.GetCoverImageAsync(readingListId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
        {
            path = await _readingListService.GenerateReadingListCoverImage(readingListId);
        }

        return PhysicalFile(path);
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
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, string apiKey, int imageOffset = 0)
    {
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, imageOffset, UserId);
        if (bookmark == null) return BadRequest(await _localizationService.Translate(UserId, "bookmark-doesnt-exist"));

        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var path = Path.Join(bookmarkDirectory, bookmark.FileName);

        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns the image associated with a web-link
    /// </summary>
    /// <param name="url"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("web-link")]
    public async Task<ActionResult> GetWebLinkImage(string url, string apiKey)
    {
        if (string.IsNullOrEmpty(url)) return BadRequest(await _localizationService.Translate(UserId, "must-be-defined", "Url"));

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
                return BadRequest(await _localizationService.Translate(UserId, "generic-favicon"));
            }
        }

        return PhysicalFile(domainFilePath);
    }


    /// <summary>
    /// Returns the image associated with a publisher
    /// </summary>
    /// <param name="publisherName"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("publisher")]
    public async Task<ActionResult> GetPublisherImage(string publisherName, string apiKey)
    {
        if (string.IsNullOrEmpty(publisherName)) return BadRequest(await _localizationService.Translate(UserId, "must-be-defined", "publisherName"));
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
                return BadRequest(await _localizationService.Translate(UserId, "generic-favicon"));
            }
        }

        return CachedFile(domainFilePath);
    }

    /// <summary>
    /// Returns cover image for Person
    /// </summary>
    /// <param name="personId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("person-cover")]
    public async Task<ActionResult> GetPersonCoverImage(int personId, string apiKey)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.UserRepository.GetPersonCoverImageAsync(personId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for User
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("user-cover")]
    public async Task<ActionResult> GetUserCoverImage(int userId, string apiKey)
    {
        var filename = await _unitOfWork.UserRepository.GetCoverImageAsync(userId, UserId);
        var path = Path.Join(_directoryService.CoverImageDirectory, filename);
        return CachedFile(path);
    }

    /// <summary>
    /// Returns a temp coverupload image
    /// </summary>
    /// <remarks>Requires Admin Role to perform upload</remarks>
    /// <param name="filename">Filename of file. This is used with upload/upload-by-url</param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("cover-upload")]
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
        return PhysicalFile(path);
    }
}
