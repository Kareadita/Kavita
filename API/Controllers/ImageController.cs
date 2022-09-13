using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for servicing up images stored in Kavita for entities
/// </summary>
[AllowAnonymous]
public class ImageController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;

    /// <inheritdoc />
    public ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
    }

    /// <summary>
    /// Returns cover image for Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-cover")]
    [ResponseCache(CacheProfileName = "Images")]
    public async Task<ActionResult> GetChapterCoverImage(int chapterId)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-cover")]
    [ResponseCache(CacheProfileName = "Images")]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = "Images")]
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        Response.AddCacheHeader(path);

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for Collection Tag
    /// </summary>
    /// <param name="collectionTagId"></param>
    /// <returns></returns>
    [HttpGet("collection-cover")]
    [ResponseCache(CacheProfileName = "Images")]
    public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
    }

    /// <summary>
    /// Returns cover image for a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("readinglist-cover")]
    [ResponseCache(CacheProfileName = "Images")]
    public async Task<ActionResult> GetReadingListCoverImage(int readingListId)
    {
        var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ReadingListRepository.GetCoverImageAsync(readingListId));
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
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
    [ResponseCache(CacheProfileName = "Images")]
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        var bookmark = await _unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, userId);
        if (bookmark == null) return BadRequest("Bookmark does not exist");

        var bookmarkDirectory =
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        var file = new FileInfo(Path.Join(bookmarkDirectory, bookmark.FileName));
        var format = Path.GetExtension(file.FullName).Replace(".", "");

        return PhysicalFile(file.FullName, "image/" + format, Path.GetFileName(file.FullName));
    }

    /// <summary>
    /// Returns a temp coverupload image
    /// </summary>
    /// <param name="filename">Filename of file. This is used with upload/upload-by-url</param>
    /// <returns></returns>
    [Authorize(Policy="RequireAdminRole")]
    [HttpGet("cover-upload")]
    [ResponseCache(CacheProfileName = "Images")]
    public ActionResult GetCoverUploadImage(string filename)
    {
        if (filename.Contains("..")) return BadRequest("Invalid Filename");

        var path = Path.Join(_directoryService.TempDirectory, filename);
        if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"File does not exist");
        var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

        return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
    }
}
