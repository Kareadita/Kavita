using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Uploads;
using API.Extensions;
using API.Services;
using API.SignalR;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

/// <summary>
///
/// </summary>
public class UploadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageService _imageService;
    private readonly ILogger<UploadController> _logger;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IDirectoryService _directoryService;
    private readonly IEventHub _eventHub;
    private readonly IReadingListService _readingListService;

    /// <inheritdoc />
    public UploadController(IUnitOfWork unitOfWork, IImageService imageService, ILogger<UploadController> logger,
        ITaskScheduler taskScheduler, IDirectoryService directoryService, IEventHub eventHub, IReadingListService readingListService)
    {
        _unitOfWork = unitOfWork;
        _imageService = imageService;
        _logger = logger;
        _taskScheduler = taskScheduler;
        _directoryService = directoryService;
        _eventHub = eventHub;
        _readingListService = readingListService;
    }

    /// <summary>
    /// This stores a file (image) in temp directory for use in a cover image replacement flow.
    /// This is automatically cleaned up.
    /// </summary>
    /// <param name="dto">Escaped url to download from</param>
    /// <returns>filename</returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("upload-by-url")]
    public async Task<ActionResult<string>> GetImageFromFile(UploadUrlDto dto)
    {
        var dateString = $"{DateTime.UtcNow.ToShortDateString()}_{DateTime.UtcNow.ToLongTimeString()}".Replace('/', '_').Replace(':', '_');
        var format = _directoryService.FileSystem.Path.GetExtension(dto.Url.Split('?')[0]).Replace(".", string.Empty);
        try
        {
            var path = await dto.Url
                .DownloadFileAsync(_directoryService.TempDirectory, $"coverupload_{dateString}.{format}");

            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
                return BadRequest($"Could not download file");

            if (!await _imageService.IsImage(path)) return BadRequest("Url does not return a valid image");

            return $"coverupload_{dateString}.{format}";
        }
        catch (FlurlHttpException ex)
        {
            // Unauthorized
            if (ex.StatusCode == 401)
                return BadRequest("The server requires authentication to load the url externally");
        }

        return BadRequest("Unable to download image, please use another url or upload by file");
    }

    /// <summary>
    /// Replaces series cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(8_000_000)]
    [HttpPost("series")]
    public async Task<ActionResult> UploadSeriesCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (string.IsNullOrEmpty(uploadFileDto.Url))
        {
            return BadRequest("You must pass a url to use");
        }

        try
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadFileDto.Id);
            if (series == null) return BadRequest("Invalid Series");
            var filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetSeriesFormat(uploadFileDto.Id)}");

            if (!string.IsNullOrEmpty(filePath))
            {
                series.CoverImage = filePath;
                series.CoverImageLocked = true;
                _unitOfWork.SeriesRepository.Update(series);
            }

            if (_unitOfWork.HasChanges())
            {
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false);
                await _unitOfWork.CommitAsync();
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Series {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to save cover image to Series");
    }

    /// <summary>
    /// Replaces collection tag cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(8_000_000)]
    [HttpPost("collection")]
    public async Task<ActionResult> UploadCollectionCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (string.IsNullOrEmpty(uploadFileDto.Url))
        {
            return BadRequest("You must pass a url to use");
        }

        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(uploadFileDto.Id);
            if (tag == null) return BadRequest("Invalid Tag id");
            var filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetCollectionTagFormat(uploadFileDto.Id)}");

            if (!string.IsNullOrEmpty(filePath))
            {
                tag.CoverImage = filePath;
                tag.CoverImageLocked = true;
                _unitOfWork.CollectionTagRepository.Update(tag);
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(tag.Id, MessageFactoryEntityTypes.CollectionTag), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Collection Tag {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to save cover image to Collection Tag");
    }

    /// <summary>
    /// Replaces reading list cover image and locks it with a base64 encoded image
    /// </summary>
    /// <remarks>This is the only API that can be called by non-admins, but the authenticated user must have a readinglist permission</remarks>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [RequestSizeLimit(8_000_000)]
    [HttpPost("reading-list")]
    public async Task<ActionResult> UploadReadingListCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (string.IsNullOrEmpty(uploadFileDto.Url))
        {
            return BadRequest("You must pass a url to use");
        }

        if (_readingListService.UserHasReadingListAccess(uploadFileDto.Id, User.GetUsername()) == null)
            return Unauthorized("You do not have access");

        try
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(uploadFileDto.Id);
            if (readingList == null) return BadRequest("Reading list is not valid");
            var filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetReadingListFormat(uploadFileDto.Id)}");

            if (!string.IsNullOrEmpty(filePath))
            {
                readingList.CoverImage = filePath;
                readingList.CoverImageLocked = true;
                _unitOfWork.ReadingListRepository.Update(readingList);
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(readingList.Id, MessageFactoryEntityTypes.ReadingList), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Reading List {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to save cover image to Reading List");
    }

    private async Task<string> CreateThumbnail(UploadFileDto uploadFileDto, string filename, int thumbnailSize = 0)
    {
        var convertToWebP = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).ConvertCoverToWebP;
        if (thumbnailSize > 0)
        {
            return _imageService.CreateThumbnailFromBase64(uploadFileDto.Url,
                filename, convertToWebP, thumbnailSize);
        }

        return _imageService.CreateThumbnailFromBase64(uploadFileDto.Url,
            filename, convertToWebP); ;
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(8_000_000)]
    [HttpPost("chapter")]
    public async Task<ActionResult> UploadChapterCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (string.IsNullOrEmpty(uploadFileDto.Url))
        {
            return BadRequest("You must pass a url to use");
        }

        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
            if (chapter == null) return BadRequest("Invalid Chapter");
            var filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetChapterFormat(uploadFileDto.Id, chapter.VolumeId)}");

            if (!string.IsNullOrEmpty(filePath))
            {
                chapter.CoverImage = filePath;
                chapter.CoverImageLocked = true;
                _unitOfWork.ChapterRepository.Update(chapter);
                var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId);
                if (volume != null)
                {
                    volume.CoverImage = chapter.CoverImage;
                    _unitOfWork.VolumeRepository.Update(volume);
                }
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(chapter.VolumeId, MessageFactoryEntityTypes.Volume), false);
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(chapter.Id, MessageFactoryEntityTypes.Chapter), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Chapter {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to save cover image to Chapter");
    }

    /// <summary>
    /// Replaces library cover image with a base64 encoded image. If empty string passed, will reset to null.
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(8_000_000)]
    [HttpPost("library")]
    public async Task<ActionResult> UploadLibraryCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(uploadFileDto.Id);
        if (library == null) return BadRequest("This library does not exist");

        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (string.IsNullOrEmpty(uploadFileDto.Url))
        {
            library.CoverImage = null;
            _unitOfWork.LibraryRepository.Update(library);
            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(library.Id, MessageFactoryEntityTypes.Library), false);
            }

            return Ok();
        }

        try
        {
            var filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetLibraryFormat(uploadFileDto.Id)}", ImageService.LibraryThumbnailWidth);

            if (!string.IsNullOrEmpty(filePath))
            {
                library.CoverImage = filePath;
                _unitOfWork.LibraryRepository.Update(library);
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(library.Id, MessageFactoryEntityTypes.Library), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Library {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to save cover image to Library");
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadFileDto">Does not use Url property</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("reset-chapter-lock")]
    public async Task<ActionResult> ResetChapterLock(UploadFileDto uploadFileDto)
    {
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
            if (chapter == null) return BadRequest("Chapter no longer exists");
            var originalFile = chapter.CoverImage;
            chapter.CoverImage = string.Empty;
            chapter.CoverImageLocked = false;
            _unitOfWork.ChapterRepository.Update(chapter);
            var volume = (await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId))!;
            volume.CoverImage = chapter.CoverImage;
            _unitOfWork.VolumeRepository.Update(volume);
            var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId))!;

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                if (originalFile != null) System.IO.File.Delete(originalFile);
                _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue resetting cover lock for Chapter {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest("Unable to resetting cover lock for Chapter");
    }

}
