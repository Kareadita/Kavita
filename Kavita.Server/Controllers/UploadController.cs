using System;
using System.IO;
using System.Linq;
using System.Threading;
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
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Uploads;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Images;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

[SkipDeviceTracking]
public class UploadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadController> _logger;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IDirectoryService _directoryService;
    private readonly IReadingListService _readingListService;
    private readonly ILocalizationService _localizationService;
    private readonly IUrlValidationService _urlValidationService;
    private readonly IImageFileService _imageFileService;
    private readonly ICompositeCoverService _compositeCoverService;

    /// <inheritdoc />
    public UploadController(IUnitOfWork unitOfWork, ILogger<UploadController> logger,
        ITaskScheduler taskScheduler, IDirectoryService directoryService, IReadingListService readingListService,
        ILocalizationService localizationService, IUrlValidationService urlValidationService,
        IImageFileService imageFileService, ICompositeCoverService compositeCoverService)
    {
        _imageFileService = imageFileService;
        _compositeCoverService = compositeCoverService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _taskScheduler = taskScheduler;
        _directoryService = directoryService;
        _readingListService = readingListService;
        _localizationService = localizationService;
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
        var ct = HttpContext.RequestAborted;
        try
        {
            await _urlValidationService.ValidateUrlAsync(dto.Url);
        }
        catch (Exception)
        {
            return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
        }

        var now = DateTime.UtcNow;
        var dateString = $"{now:d}_{now:T}".Replace('/', '_').Replace(':', '_');
        var stem = $"coverupload_{dateString}_{Guid.NewGuid():N}";
        var downloadPath = Path.Join(_directoryService.TempDirectory, stem + ".tmp");
        try
        {
            await FlurlConfiguration.DownloadToFileAsync(dto.Url, downloadPath, ControllerConstants.MaxUploadSizeBytes, ct);

            var extension = ImageHelper.GetImageExtension(downloadPath);
            if (extension == null)
            {
                _logger.LogWarning("Rejected cover from {Url} - content is not a supported image", dto.Url.Sanitize());
                return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
            }

            var fileName = stem + extension;
            _directoryService.FileSystem.File.Move(downloadPath, Path.Join(_directoryService.TempDirectory, fileName));

            return fileName;
        }
        catch (Exception ex) when (ex is FlurlHttpException or KavitaException)
        {
            _logger.LogWarning(ex, "Could not download a cover from {Url}", dto.Url.Sanitize());
            return BadRequest(await _localizationService.TranslateAsync(UserId, "url-not-valid"));
        }
        finally
        {
            _directoryService.DeleteFiles([downloadPath]);
        }
    }

    [HttpPost("upload-chapter-cover")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<string>> GetImageFromChapterCover([FromQuery] int chapterId)
    {
        var ct = HttpContext.RequestAborted;
        var fileName = await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId, ct);
        if (string.IsNullOrEmpty(fileName))
        {
            return NotFound();
        }

        return CopyCoverImageIntoTemp(fileName);
    }

    [HttpPost("upload-volume-cover")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<string>> GetImageFromVolumeCover([FromQuery] int volumeId)
    {
        var ct = HttpContext.RequestAborted;
        var fileName = await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId, ct);
        if (string.IsNullOrEmpty(fileName))
        {
            return NotFound();
        }

        return CopyCoverImageIntoTemp(fileName);
    }

    [HttpPost("upload-series-cover")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<string>> GetImageFromSeriesCover([FromQuery] int seriesId)
    {
        var ct = HttpContext.RequestAborted;
        var fileName = await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId, ct);
        if (string.IsNullOrEmpty(fileName))
        {
            return NotFound();
        }

        return CopyCoverImageIntoTemp(fileName);
    }

    private ActionResult<string> CopyCoverImageIntoTemp(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var sourcePath = Path.Join(_directoryService.CoverImageDirectory, safeFileName);
        if (!_directoryService.FileSystem.File.Exists(sourcePath)) return NotFound();

        var finalFileName = $"coverupload_{Guid.NewGuid():N}_{safeFileName}";

        var tempPath = Path.Join(_directoryService.TempDirectory, finalFileName);

        _directoryService.FileSystem.File.Copy(sourcePath, tempPath, true);

        return Ok(finalFileName);
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
        if (!Parser.IsImage(file.FileName))
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

            if (!_directoryService.FileSystem.File.Exists(path) || ImageHelper.GetImageExtension(path) == null)
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
        var ct = HttpContext.RequestAborted;
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        try
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadCoverFileDto.Id, SeriesIncludes.CoverImageFile, ct);

            if (series == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "series-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(series, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(series, ct: ct);
                await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
            }

            return Ok();
        }
        catch (KavitaException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Series {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        var ct = HttpContext.RequestAborted;
        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(uploadCoverFileDto.Id,
                CollectionIncludes.CoverImageFile, ct);
            if (tag == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "collection-doesnt-exist"));

            if (!User.IsInRole(PolicyConstants.AdminRole) && tag.AppUserId != UserId)
                return Unauthorized();

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(tag, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(tag, ct: ct);
                await _compositeCoverService.CreateIfMissingAsync(ImageKind.CollectionCover, tag.Id, ct);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Collection Tag {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        if (await _readingListService.UserHasReadingListAccess(uploadCoverFileDto.Id, Username!) == null)
            return Unauthorized(await _localizationService.TranslateAsync(UserId, "access-denied"));

        try
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(uploadCoverFileDto.Id,
                ReadingListIncludes.CoverImageFile, ct);
            if (readingList == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "reading-list-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(readingList, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(readingList, ct: ct);
                await _compositeCoverService.CreateIfMissingAsync(ImageKind.ReadingListCover, readingList.Id, ct);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Reading List {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-reading-list-save"));
    }

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

    /// <returns>Null when the DTO carries neither a staged file nor base64, which means reset</returns>
    private async Task<ImageSource?> GetCoverSourceAsync(UploadCoverFileDto uploadCoverFileDto)
    {
        if (!string.IsNullOrEmpty(uploadCoverFileDto.FileName))
        {
            var tempPath = ResolveTempCoverPath(uploadCoverFileDto.FileName)
                           ?? throw new KavitaException(await _localizationService.TranslateAsync(UserId, "invalid-filename"));

            return new ImageSource.FromFile(tempPath);
        }

        if (string.IsNullOrEmpty(uploadCoverFileDto.Url)) return null;

        try
        {
            return new ImageSource.FromBytes(Convert.FromBase64String(uploadCoverFileDto.Url));
        }
        catch (FormatException e)
        {
            throw new KavitaException("Invalid Base64 string", e);
        }
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
        var ct = HttpContext.RequestAborted;
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadCoverFileDto.Id,
                ChapterIncludes.Volumes | ChapterIncludes.CoverImageFile, ct);
            if (chapter == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(chapter, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(chapter, ct: ct);
                var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(chapter.Volume.SeriesId, SeriesIncludes.None, ct))!;
                await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Chapter {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        // Check if Url is non-empty, request the image and place in temp, then ask image service to handle it.
        // See if we can do this all in memory without touching underlying system
        try
        {
            var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(uploadCoverFileDto.Id, VolumeIncludes.CoverImageFile, ct);
            if (volume == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(volume, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(volume, ct: ct);
                var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId, SeriesIncludes.None, ct))!;
                await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Volume {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(uploadCoverFileDto.Id, LibraryIncludes.CoverImageFile, ct);
        if (library == null) return BadRequest("This library does not exist");

        try
        {
            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(library, source, new CoverWrite(ImageOrigin.Upload, Lock: false), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(library, ct: ct);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Library {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        try
        {
            var person = await _unitOfWork.PersonRepository.GetPersonById(uploadCoverFileDto.Id, PersonIncludes.CoverImageFile, ct);
            if (person == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "person-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(person, source,
                    new CoverWrite(ImageOrigin.Upload, Lock: uploadCoverFileDto.LockCover), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(person, ct: ct);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for Person {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        try
        {
            if (uploadCoverFileDto.Id != UserId) return NotFound();

            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(uploadCoverFileDto.Id, AppUserIncludes.CoverImageFile, ct);
            if (user == null) return BadRequest(await _localizationService.TranslateAsync(UserId, "user-doesnt-exist"));

            if (await GetCoverSourceAsync(uploadCoverFileDto) is { } source)
            {
                await _imageFileService.SetCoverAsync(user, source, new CoverWrite(ImageOrigin.Upload, Lock: false), ct: ct);
            }
            else
            {
                await _imageFileService.ClearCoverAsync(user, ct: ct);
            }

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue uploading cover image for User {Id}", uploadCoverFileDto.Id);
            await _unitOfWork.RollbackAsync(ct);
        }

        return BadRequest(await _localizationService.TranslateAsync(UserId, "generic-cover-person-save"));
    }

}
