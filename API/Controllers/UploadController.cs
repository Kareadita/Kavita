using System;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Uploads;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.Services.Tasks.Metadata;
using API.SignalR;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

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
    private readonly ILocalizationService _localizationService;
    private readonly ICoverDbService _coverDbService;

    /// <inheritdoc />
    public UploadController(IUnitOfWork unitOfWork, IImageService imageService, ILogger<UploadController> logger,
        ITaskScheduler taskScheduler, IDirectoryService directoryService, IEventHub eventHub, IReadingListService readingListService,
        ILocalizationService localizationService, ICoverDbService coverDbService)
    {
        _unitOfWork = unitOfWork;
        _imageService = imageService;
        _logger = logger;
        _taskScheduler = taskScheduler;
        _directoryService = directoryService;
        _eventHub = eventHub;
        _readingListService = readingListService;
        _localizationService = localizationService;
        _coverDbService = coverDbService;
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
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "url-not-valid"));

            if (!await _imageService.IsImage(path)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "url-not-valid"));

            return $"coverupload_{dateString}.{format}";
        }
        catch (FlurlHttpException ex)
        {
            // Unauthorized
            if (ex.StatusCode == 401)
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "url-not-valid"));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "url-not-valid"));
    }

    /// <summary>
    /// Replaces series cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("series")]
    public async Task<ActionResult> UploadSeriesCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadFileDto.Id);

            if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "series-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (!string.IsNullOrEmpty(uploadFileDto.Url))
            {
                filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetSeriesFormat(uploadFileDto.Id)}");
                lockState = uploadFileDto.LockCover;
            }

            series.CoverImage = filePath;
            series.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(series);
            _unitOfWork.SeriesRepository.Update(series);

            if (_unitOfWork.HasChanges())
            {
                // Refresh covers
                if (string.IsNullOrEmpty(uploadFileDto.Url))
                {
                    _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                }

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-series-save"));
    }

    /// <summary>
    /// Replaces collection tag cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("collection")]
    public async Task<ActionResult> UploadCollectionCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(uploadFileDto.Id);
            if (tag == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "collection-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (!string.IsNullOrEmpty(uploadFileDto.Url))
            {
                filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetCollectionTagFormat(uploadFileDto.Id)}");
                lockState = uploadFileDto.LockCover;
            }

            tag.CoverImage = filePath;
            tag.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(tag);
            _unitOfWork.CollectionTagRepository.Update(tag);

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-collection-save"));
    }

    /// <summary>
    /// Replaces reading list cover image and locks it with a base64 encoded image
    /// </summary>
    /// <remarks>This is the only API that can be called by non-admins, but the authenticated user must have a readinglist permission</remarks>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("reading-list")]
    public async Task<ActionResult> UploadReadingListCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (await _readingListService.UserHasReadingListAccess(uploadFileDto.Id, User.GetUsername()) == null)
            return Unauthorized(await _localizationService.Translate(User.GetUserId(), "access-denied"));

        try
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(uploadFileDto.Id);
            if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));


            var filePath = string.Empty;
            var lockState = false;
            if (!string.IsNullOrEmpty(uploadFileDto.Url))
            {
                filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetReadingListFormat(uploadFileDto.Id)}");
                lockState = uploadFileDto.LockCover;
            }


            readingList.CoverImage = filePath;
            readingList.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(readingList);
            _unitOfWork.ReadingListRepository.Update(readingList);

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-reading-list-save"));
    }

    private async Task<string> CreateThumbnail(UploadFileDto uploadFileDto, string filename)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        return _imageService.CreateThumbnailFromBase64(uploadFileDto.Url,
            filename, encodeFormat, coverImageSize.GetDimensions().Width);
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("chapter")]
    public async Task<ActionResult> UploadChapterCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
            if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (!string.IsNullOrEmpty(uploadFileDto.Url))
            {
                filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetChapterFormat(uploadFileDto.Id, chapter.VolumeId)}");
                lockState = uploadFileDto.LockCover;
            }

            chapter.CoverImage = filePath;
            chapter.CoverImageLocked = lockState;
            _unitOfWork.ChapterRepository.Update(chapter);
            var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId);
            if (volume != null)
            {
                volume.CoverImage = chapter.CoverImage;
                volume.CoverImageLocked = lockState;
                _unitOfWork.VolumeRepository.Update(volume);
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();

                // Refresh covers
                if (string.IsNullOrEmpty(uploadFileDto.Url))
                {
                    var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume!.SeriesId))!;
                    _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                }


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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-chapter-save"));
    }

    /// <summary>
    /// Replaces volume cover image and locks it with a base64 encoded image.
    /// </summary>
    /// <remarks>This will not update the underlying chapter</remarks>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("volume")]
    public async Task<ActionResult> UploadVolumeCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(uploadFileDto.Id, VolumeIncludes.Chapters);
            if (volume == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "volume-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (!string.IsNullOrEmpty(uploadFileDto.Url))
            {
                filePath = await CreateThumbnail(uploadFileDto, $"{ImageService.GetVolumeFormat(uploadFileDto.Id)}");
                lockState = uploadFileDto.LockCover;
            }

            volume.CoverImage = filePath;
            volume.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(volume);
            _unitOfWork.VolumeRepository.Update(volume);

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();

                // Refresh covers
                if (string.IsNullOrEmpty(uploadFileDto.Url))
                {
                    var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId))!;
                    _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                }


                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(uploadFileDto.Id, MessageFactoryEntityTypes.Volume), false);
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Chapter), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Volume {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-volume-save"));
    }


    /// <summary>
    /// Replaces library cover image with a base64 encoded image. If empty string passed, will reset to null.
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
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
            library.ResetColorScape();
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
            var filePath = await CreateThumbnail(uploadFileDto,
                $"{ImageService.GetLibraryFormat(uploadFileDto.Id)}");

            if (!string.IsNullOrEmpty(filePath))
            {
                library.CoverImage = filePath;
                _imageService.UpdateColorScape(library);
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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-library-save"));
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadFileDto">Does not use Url property</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("reset-chapter-lock")]
    [Obsolete("Use LockCover in UploadFileDto")]
    public async Task<ActionResult> ResetChapterLock(UploadFileDto uploadFileDto)
    {
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
            if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "reset-chapter-lock"));
    }

    /// <summary>
    /// Replaces person tag cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("person")]
    public async Task<ActionResult> UploadPersonCoverImageFromUrl(UploadFileDto uploadFileDto)
    {
        try
        {
            var person = await _unitOfWork.PersonRepository.GetPersonById(uploadFileDto.Id);
            if (person == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "person-doesnt-exist"));

            await _coverDbService.SetPersonCoverByUrl(person, uploadFileDto.Url, true);
            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Person {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-cover-person-save"));
    }


}
