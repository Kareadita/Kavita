using System;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Uploads;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Server.Attributes;
using Kavita.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

[SkipDeviceTracking]
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
    private readonly IUrlValidationService _urlValidationService;

    /// <summary>
    /// Image extensions accepted for direct file uploads. Mirrors the cover chooser's client-side allowlist and
    /// deliberately excludes SVG (script-bearing XSS vector).
    /// </summary>
    private static readonly string[] AllowedCoverExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif"];

    /// <inheritdoc />
    public UploadController(IUnitOfWork unitOfWork, IImageService imageService, ILogger<UploadController> logger,
        ITaskScheduler taskScheduler, IDirectoryService directoryService, IEventHub eventHub, IReadingListService readingListService,
        ILocalizationService localizationService, ICoverDbService coverDbService, IUrlValidationService urlValidationService)
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
        _urlValidationService = urlValidationService;
    }

    /// <summary>
    /// This stores a file (image) in temp directory for use in a cover image replacement flow.
    /// This is automatically cleaned up.
    /// </summary>
    /// <param name="dto">Escaped url to download from</param>
    /// <returns>filename</returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("upload-by-url")]
    public async Task<ActionResult<string>> GetImageFromFile(UploadUrlDto dto)
    {
        try
        {
            // We need to allow any images that are coming from within Kavita to bypass Validation
            if (!dto.IsInternalUrl)
            {
                await _urlValidationService.ValidateUrlAsync(dto.Url);
            }
        }
        catch (Exception)
        {
            return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
        }

        var now = DateTime.UtcNow;
        var dateString = $"{now:d}_{now:T}".Replace('/', '_').Replace(':', '_');
        try
        {
            var format = await dto.Url.GetFileFormatAsync();
            if (string.IsNullOrEmpty(format))
            {
                // Fallback to unreliable parsing if needed
                format = _directoryService.FileSystem.Path.GetExtension(dto.Url.Split('?')[0]).Replace(".", string.Empty);
            }

            var path = await dto.Url
                .DownloadFileAsync(_directoryService.TempDirectory, $"coverupload_{dateString}.{format}");

            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
                return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));

            if (!await _imageService.IsImage(path)) return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));

            return $"coverupload_{dateString}.{format}";
        }
        catch (FlurlHttpException ex)
        {
            // Unauthorized
            if (ex.StatusCode == 401)
                return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
    }

    /// <summary>
    /// Stages an uploaded image file in the temp directory for use in a cover image replacement flow.
    /// This is automatically cleaned up.
    /// </summary>
    /// <param name="file">The image file to stage</param>
    /// <returns>The generated temp filename, to be passed as <c>FileName</c> to a cover upload endpoint</returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("upload-by-file")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    public async Task<ActionResult<string>> UploadCoverByFile(IFormFile file)
    {
        if (file.Length == 0) return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));

        // Reject anything that isn't an allowed image extension before we write it to disk, so an executable (or an
        // image-content polyglot wearing a dangerous extension) never lands in temp. Content is still validated below.
        var extension = _directoryService.FileSystem.Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedCoverExtensions.Contains(extension.ToLowerInvariant()))
        {
            _logger.LogWarning("Rejected cover upload with disallowed extension '{Extension}' (file '{FileName}')",
                extension.Sanitize(), file.FileName.Sanitize());
            return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
        }

        var now = DateTime.UtcNow;
        var dateString = $"{now:d}_{now:T}".Replace('/', '_').Replace(':', '_');

        // Generate our own safe filename rather than trusting the client-supplied name. The Guid suffix avoids
        // collisions when several files are dropped within the same second.
        var fileName = $"coverupload_{dateString}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        try
        {
            var path = await UploadToTempAsync(file, fileName);

            if (!_directoryService.FileSystem.File.Exists(path) || !await _imageService.IsImage(path))
            {
                _directoryService.DeleteFiles([path]);
                _logger.LogWarning("Rejected cover upload '{FileName}' - content is not a valid image", file.FileName.Sanitize());
                return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
            }

            return fileName;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue staging an uploaded cover image file");
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
    }

    /// <summary>
    /// Replaces series cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("series")]
    public async Task<ActionResult> UploadSeriesCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadCoverFileDto.Id);

            if (series == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "series-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (HasCoverSource(uploadCoverFileDto))
            {
                filePath = await CreateThumbnail(uploadCoverFileDto, $"{ImageService.GetSeriesFormat(uploadCoverFileDto.Id)}");
                lockState = uploadCoverFileDto.LockCover;
            }

            series.CoverImage = filePath;
            series.CoverImageLocked = lockState;
            series.Metadata.KPlusOverrides.Remove(MetadataSettingField.Covers);
            _imageService.UpdateColorScape(series);
            _unitOfWork.SeriesRepository.Update(series);
            _unitOfWork.SeriesRepository.Update(series.Metadata);

            if (_unitOfWork.HasChanges())
            {
                // Refresh covers
                if (!HasCoverSource(uploadCoverFileDto))
                {
                    await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                }

                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false);
                await _unitOfWork.CommitAsync();
                return Ok();
            }

        }
        catch (KavitaException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Series {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-series-save"));
    }

    /// <summary>
    /// Replaces collection tag cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [HttpPost("collection")]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    public async Task<ActionResult> UploadCollectionCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(uploadCoverFileDto.Id);
            if (tag == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "collection-doesnt-exist"));

            if (!User.IsInRole(PolicyConstants.AdminRole) && tag.AppUserId != UserId)
                return Unauthorized();

            var filePath = string.Empty;
            var lockState = false;
            if (HasCoverSource(uploadCoverFileDto))
            {
                filePath = await CreateThumbnail(uploadCoverFileDto, $"{ImageService.GetCollectionTagFormat(uploadCoverFileDto.Id)}");
                lockState = uploadCoverFileDto.LockCover;
            }

            tag.CoverImage = filePath;
            tag.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(tag);
            _unitOfWork.CollectionTagRepository.Update(tag);

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(tag.Id, MessageFactoryEntityTypes.Collection), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Collection Tag {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-collection-save"));
    }

    /// <summary>
    /// Replaces reading list cover image and locks it with a base64 encoded image
    /// </summary>
    /// <remarks>This is the only API that can be called by non-admins, but the authenticated user must have a readinglist permission</remarks>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("reading-list")]
    public async Task<ActionResult> UploadReadingListCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (await _readingListService.UserHasReadingListAccess(uploadCoverFileDto.Id, Username!) == null)
            return Unauthorized(await _localizationService.TranslateAsync(UserId, "access-denied"));

        try
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(uploadCoverFileDto.Id);
            if (readingList == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "reading-list-doesnt-exist"));


            var filePath = string.Empty;
            var lockState = false;
            if (HasCoverSource(uploadCoverFileDto))
            {
                filePath = await CreateThumbnail(uploadCoverFileDto, $"{ImageService.GetReadingListFormat(uploadCoverFileDto.Id)}");
                lockState = uploadCoverFileDto.LockCover;
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
            _logger.LogError(e, "There was an issue uploading cover image for Reading List {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-reading-list-save"));
    }

    /// <summary>
    /// Returns true when the request carries a new cover source (a staged temp file or a base64 payload).
    /// When false, the caller is resetting/clearing the cover.
    /// </summary>
    private static bool HasCoverSource(UploadCoverFileDto dto) =>
        !string.IsNullOrEmpty(dto.FileName) || !string.IsNullOrEmpty(dto.Url);

    /// <summary>
    /// Resolves a staged temp file to its absolute path, validating it stays within the temp directory.
    /// </summary>
    /// <returns>Absolute path to the temp file, or null if invalid/missing.</returns>
    private string? ResolveTempCoverPath(string fileName)
    {
        if (!IsPathWithinDirectory(_directoryService.TempDirectory, fileName)) return null;

        var path = _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, fileName);
        return _directoryService.FileSystem.File.Exists(path) ? path : null;
    }

    private async Task<string> CreateThumbnail(UploadCoverFileDto uploadCoverFileDto, string filename)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        // Preferred path: the image was already streamed into temp (upload-by-url / upload-by-file) and we only
        // received its filename. This avoids posting a large base64 payload back through the request body.
        if (!string.IsNullOrEmpty(uploadCoverFileDto.FileName))
        {
            var tempPath = ResolveTempCoverPath(uploadCoverFileDto.FileName)
                           ?? throw new KavitaException(await _localizationService.TranslateAsync(UserId, "invalid-filename"));

            return _imageService.CreateThumbnailFromFile(tempPath,
                filename, encodeFormat, coverImageSize.GetDimensions().Width);
        }

        // Legacy fallback: base64 payload
        return _imageService.CreateThumbnailFromBase64(uploadCoverFileDto.Url!,
            filename, encodeFormat, coverImageSize.GetDimensions().Width);
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("chapter")]
    public async Task<ActionResult> UploadChapterCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadCoverFileDto.Id);
            if (chapter == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (HasCoverSource(uploadCoverFileDto))
            {
                filePath = await CreateThumbnail(uploadCoverFileDto, $"{ImageService.GetChapterFormat(uploadCoverFileDto.Id, chapter.VolumeId)}");
                lockState = uploadCoverFileDto.LockCover;
            }

            chapter.CoverImage = filePath;
            chapter.CoverImageLocked = lockState;
            chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterCovers);
            _unitOfWork.ChapterRepository.Update(chapter);
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId);
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
                if (!HasCoverSource(uploadCoverFileDto))
                {
                    var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume!.SeriesId))!;
                    await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
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
            _logger.LogError(e, "There was an issue uploading cover image for Chapter {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-chapter-save"));
    }

    /// <summary>
    /// Replaces volume cover image and locks it with a base64 encoded image.
    /// </summary>
    /// <remarks>This will not update the underlying chapter</remarks>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("volume")]
    public async Task<ActionResult> UploadVolumeCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(uploadCoverFileDto.Id, VolumeIncludes.Chapters);
            if (volume == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));

            var filePath = string.Empty;
            var lockState = false;
            if (HasCoverSource(uploadCoverFileDto))
            {
                filePath = await CreateThumbnail(uploadCoverFileDto, $"{ImageService.GetVolumeFormat(uploadCoverFileDto.Id)}");
                lockState = uploadCoverFileDto.LockCover;
            }

            volume.CoverImage = filePath;
            volume.CoverImageLocked = lockState;
            _imageService.UpdateColorScape(volume);
            _unitOfWork.VolumeRepository.Update(volume);

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();

                // Refresh covers
                if (!HasCoverSource(uploadCoverFileDto))
                {
                    var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId))!;
                    await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                }


                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(uploadCoverFileDto.Id, MessageFactoryEntityTypes.Volume), false);
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Chapter), false);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Volume {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-volume-save"));
    }


    /// <summary>
    /// Replaces library cover image with a base64 encoded image. If empty string passed, will reset to null.
    /// </summary>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("library")]
    public async Task<ActionResult> UploadLibraryCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(uploadCoverFileDto.Id);
        if (library == null) return BadRequest("This library does not exist");

        // No new cover source provided - reset the cover.
        if (!HasCoverSource(uploadCoverFileDto))
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
            var filePath = await CreateThumbnail(uploadCoverFileDto,
                $"{ImageService.GetLibraryFormat(uploadCoverFileDto.Id)}");

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
            _logger.LogError(e, "There was an issue uploading cover image for Library {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-library-save"));
    }


    /// <summary>
    /// Replaces person tag cover image and locks it with a base64 encoded image
    /// </summary>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    [HttpPost("person")]
    public async Task<ActionResult> UploadPersonCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        try
        {
            var person = await _unitOfWork.PersonRepository.GetPersonById(uploadCoverFileDto.Id);
            if (person == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "person-doesnt-exist"));

            // Person covers flow through CoverDbService (placeholder/quality checks). When the image was staged in
            // temp, bridge it to the existing base64 path - person covers are small, so the in-process encode is cheap
            // and keeps CoverDbService untouched.
            if (!string.IsNullOrEmpty(uploadCoverFileDto.FileName))
            {
                var tempPath = ResolveTempCoverPath(uploadCoverFileDto.FileName);
                if (tempPath == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "invalid-filename"));

                var base64 = Convert.ToBase64String(await _directoryService.FileSystem.File.ReadAllBytesAsync(tempPath));
                await _coverDbService.SetPersonCoverByUrl(person, base64, fromBase64: true, chooseBetterImage: false);
            }
            else
            {
                await _coverDbService.SetPersonCoverByUrl(person, uploadCoverFileDto.Url ?? string.Empty, chooseBetterImage: false);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Person {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-person-save"));
    }


    /// <summary>
    /// Replaces user cover image and locks it with a base64 encoded image
    /// </summary>
    /// <remarks>You MUST be the user in question</remarks>
    /// <param name="uploadCoverFileDto"></param>
    /// <returns></returns>
    [HttpPost("user")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [RequestSizeLimit(ControllerConstants.MaxUploadSizeBytes)]
    public async Task<ActionResult> UploadUserCoverImageFromUrl(UploadCoverFileDto uploadCoverFileDto)
    {
        try
        {
            if (uploadCoverFileDto.Id != UserId) return NotFound();

            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(uploadCoverFileDto.Id);
            if (user == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "user-doesnt-exist"));

            await _coverDbService.SetUserCoverByUrl(user, uploadCoverFileDto.Url ?? string.Empty, chooseBetterImage: false);
            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for User {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-person-save"));
    }

}
