using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.ReadingLists;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.Images;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Kavita.Services.Helpers;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for servicing up images stored in Kavita for entities
/// </summary>
/// <inheritdoc />
[SkipDeviceTracking]
public class ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService,
    ILocalizationService localizationService, ICoverDbService coverDbService,
    IExternalMetadataService externalMetadataService, IImageFileService imageFileService) : BaseApiController
{

    /// <summary>
    /// Returns cover image for Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-cover")]
    public async Task<ActionResult> GetChapterCoverImage(int chapterId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [LibraryAccess]
    [HttpGet("library-cover")]
    public async Task<ActionResult> GetLibraryCoverImage(int libraryId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.LibraryRepository.GetLibraryCoverImageAsync(libraryId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume-cover")]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Collection
    /// </summary>
    /// <param name="collectionTagId"></param>
    /// <returns></returns>
    [HttpGet("collection-cover")]
    public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId)
    {
        var ct = HttpContext.RequestAborted;
        var collectionTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionTagId, ct: ct);
        if (collectionTag == null || (collectionTag.AppUserId != UserId && !collectionTag.Promoted)) return NotFound();

        var cover = await unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("readinglist-cover")]
    public async Task<ActionResult> GetReadingListCoverImage(int readingListId)
    {
        var ct = HttpContext.RequestAborted;
        var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId, ct: ct);
        if (readingList == null || (readingList.AppUserId != UserId && !readingList.Promoted)) return NotFound();

        var cover = await unitOfWork.ReadingListRepository.GetCoverImageAsync(readingListId, ct);
        if (string.IsNullOrEmpty(cover)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns image for a given bookmark page
    /// </summary>
    /// <remarks>This request is served unauthenticated, but user must be passed via api key to validate</remarks>
    /// <param name="chapterId"></param>
    /// <param name="pageNum">Starts at 0</param>
    /// <param name="imageOffset">Only applicable for Epubs - handles multiple images on one page</param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("bookmark")]
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, int imageOffset = 0)
    {
        var ct = HttpContext.RequestAborted;
        var bookmark = await unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, imageOffset, UserId, ct);
        if (bookmark?.ImageFile == null) return NotFound();

        var path = Path.Join(await imageFileService.GetImageDirectoryAsync(ImageKind.Bookmark, ct), bookmark.ImageFile.FileName);

        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns the image associated with a web-link
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    [HttpGet("web-link")]
    public async Task<ActionResult> GetWebLinkImage(string url)
    {
        var ct = HttpContext.RequestAborted;
        if (string.IsNullOrEmpty(url)) return BadRequest(await localizationService.TranslateAsync(UserId, "must-be-defined", "Url"));

        var encodeFormat = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        var webLinkFileName = ImageHelper.GetWebLinkFormat(url, encodeFormat);
        if (!IsPathWithinDirectory(directoryService.FaviconDirectory, webLinkFileName)) return BadRequest();

        // Check if the domain exists
        var domainFilePath = directoryService.FileSystem.Path.Join(directoryService.FaviconDirectory, webLinkFileName);
        if (!directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = directoryService.FileSystem.Path.Join(directoryService.FaviconDirectory,
                    await coverDbService.DownloadFaviconAsync(url, encodeFormat, ct));
            }
            catch (Exception)
            {
                return BadRequest(await localizationService.TranslateAsync(UserId, "generic-favicon"));
            }
        }

        return PhysicalFile(domainFilePath);
    }


    /// <summary>
    /// Returns the image associated with a publisher
    /// </summary>
    /// <param name="publisherName"></param>
    /// <returns></returns>
    [HttpGet("publisher")]
    public async Task<ActionResult> GetPublisherImage(string publisherName)
    {
        var ct = HttpContext.RequestAborted;
        if (string.IsNullOrEmpty(publisherName)) return BadRequest(await localizationService.TranslateAsync(UserId, "must-be-defined", "publisherName"));

        var encodeFormat = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).EncodeMediaAs;

        var publisherFileName = ImageHelper.GetPublisherFormat(publisherName, encodeFormat);
        if (!IsPathWithinDirectory(directoryService.PublisherDirectory, publisherFileName)) return BadRequest();

        // Check if the domain exists
        var domainFilePath = directoryService.FileSystem.Path.Join(directoryService.PublisherDirectory, publisherFileName);
        if (!directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = directoryService.FileSystem.Path.Join(directoryService.PublisherDirectory,
                    await coverDbService.DownloadPublisherImageAsync(publisherName, encodeFormat, ct));
            }
            catch (Exception)
            {
                return BadRequest(await localizationService.TranslateAsync(UserId, "generic-favicon"));
            }
        }

        if (string.IsNullOrEmpty(domainFilePath))
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "generic-favicon"));
        }

        return CachedFile(domainFilePath);
    }

    /// <summary>
    /// Returns cover image for Person
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [PersonAccess]
    [HttpGet("person-cover")]
    public async Task<ActionResult> GetPersonCoverImage(int personId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.UserRepository.GetPersonCoverImageAsync(personId, ct);
        if (cover == null) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for User
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("user-cover")]
    public async Task<ActionResult> GetUserCoverImage(int userId)
    {
        var ct = HttpContext.RequestAborted;
        var cover = await unitOfWork.UserRepository.GetCoverImageAsync(userId, ct);
        if (cover == null) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, cover);
        return CachedFile(path);
    }

    /// <summary>
    /// Returns a temp coverupload image
    /// </summary>
    /// <remarks>Requires Admin Role to perform upload</remarks>
    /// <param name="filename">Filename of file. This is used with upload/upload-by-url</param>
    /// <returns></returns>
    [HttpGet("cover-upload")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> GetCoverUploadImage(string filename)
    {
        if (!IsPathWithinDirectory(directoryService.TempDirectory, filename)) return BadRequest(await localizationService.TranslateAsync(UserId, "invalid-filename"));

        var path = Path.Join(directoryService.TempDirectory, filename);
        return PhysicalFile(path);
    }


    [HttpGet("external/series")]
    [Authorize(PolicyGroups.AdminPolicy)]
    [SeriesAccess]
    public async Task<ActionResult<List<ExternalCoverResponseDto>>> GetExternalCoverImagesForSeries(int seriesId)
    {
        return Ok(await externalMetadataService.GetExternalCovers(seriesId, null, null, HttpContext.RequestAborted));
    }

    [HttpGet("external/volume")]
    [Authorize(PolicyGroups.AdminPolicy)]
    [VolumeAccess]
    public async Task<ActionResult<List<ExternalCoverResponseDto>>> GetExternalCoverImagesForVolume(int seriesId, int volumeId)
    {
        return Ok(await externalMetadataService.GetExternalCovers(seriesId, volumeId, null, HttpContext.RequestAborted));
    }

    [HttpGet("external/chapter")]
    [Authorize(PolicyGroups.AdminPolicy)]
    [ChapterAccess]
    public async Task<ActionResult<List<ExternalCoverResponseDto>>> GetExternalCoverImagesForChapter(int seriesId, int chapterId)
    {
        return Ok(await externalMetadataService.GetExternalCovers(seriesId, null, chapterId, HttpContext.RequestAborted));
    }
}
