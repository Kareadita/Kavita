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
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for servicing up images stored in Kavita for entities
/// </summary>
/// <inheritdoc />
[SkipDeviceTracking]
public class ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService,
    ILocalizationService localizationService, IReadingListService readingListService,
    ICoverDbService coverDbService, ICollectionTagService collectionTagService,
    IExternalMetadataService externalMetadataService) : BaseApiController
{

    /// <summary>
    /// Returns cover image for Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-cover")]
    public async Task<ActionResult> GetChapterCoverImage(int chapterId, string apiKey)
    {
        var path = Path.Join(directoryService.CoverImageDirectory, await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [LibraryAccess]
    [HttpGet("library-cover")]
    public async Task<ActionResult> GetLibraryCoverImage(int libraryId, string apiKey)
    {
        var path = Path.Join(directoryService.CoverImageDirectory, await unitOfWork.LibraryRepository.GetLibraryCoverImageAsync(libraryId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume-cover")]
    public async Task<ActionResult> GetVolumeCoverImage(int volumeId, string apiKey)
    {
        var path = Path.Join(directoryService.CoverImageDirectory, await unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
        return PhysicalFile(path);
    }

    /// <summary>
    /// Returns cover image for Series
    /// </summary>
    /// <param name="seriesId">Id of Series</param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("series-cover")]
    public async Task<ActionResult> GetSeriesCoverImage(int seriesId, string apiKey)
    {
        var path = Path.Join(directoryService.CoverImageDirectory, await unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
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
        var collectionTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionTagId, ct: HttpContext.RequestAborted);
        if (collectionTag == null || (collectionTag.AppUserId != UserId && !collectionTag.Promoted)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, collectionTag.CoverImage);
        if (string.IsNullOrEmpty(path) || !directoryService.FileSystem.File.Exists(path))
        {
            path = await collectionTagService.GenerateCollectionCoverImage(collectionTagId);
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
        var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId, ct: HttpContext.RequestAborted);
        if (readingList == null || (readingList.AppUserId != UserId && !readingList.Promoted)) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, readingList.CoverImage);
        if (string.IsNullOrEmpty(path) || !directoryService.FileSystem.File.Exists(path))
        {
            readingList.CoverImage = await readingListService.GenerateReadingListCoverImage(readingListId);
            path = Path.Join(directoryService.CoverImageDirectory, readingList.CoverImage);
            await unitOfWork.CommitAsync();
        }

        return PhysicalFile(path);
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
    [ChapterAccess]
    [HttpGet("bookmark")]
    public async Task<ActionResult> GetBookmarkImage(int chapterId, int pageNum, string apiKey, int imageOffset = 0)
    {
        var bookmark = await unitOfWork.UserRepository.GetBookmarkForPage(pageNum, chapterId, imageOffset, UserId);
        if (bookmark == null) return BadRequest(await localizationService.TranslateAsync(UserId, "bookmark-doesnt-exist"));

        var bookmarkDirectory =
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
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
        if (string.IsNullOrEmpty(url)) return BadRequest(await localizationService.TranslateAsync(UserId, "must-be-defined", "Url"));

        var encodeFormat = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        var webLinkFileName = ImageService.GetWebLinkFormat(url, encodeFormat);
        if (!IsPathWithinDirectory(directoryService.FaviconDirectory, webLinkFileName)) return BadRequest();

        // Check if the domain exists
        var domainFilePath = directoryService.FileSystem.Path.Join(directoryService.FaviconDirectory, webLinkFileName);
        if (!directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = directoryService.FileSystem.Path.Join(directoryService.FaviconDirectory,
                    await coverDbService.DownloadFaviconAsync(url, encodeFormat));
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
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpGet("publisher")]
    public async Task<ActionResult> GetPublisherImage(string publisherName, string apiKey)
    {
        if (string.IsNullOrEmpty(publisherName)) return BadRequest(await localizationService.TranslateAsync(UserId, "must-be-defined", "publisherName"));

        var encodeFormat = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

        var publisherFileName = ImageService.GetPublisherFormat(publisherName, encodeFormat);
        if (!IsPathWithinDirectory(directoryService.PublisherDirectory, publisherFileName)) return BadRequest();

        // Check if the domain exists
        var domainFilePath = directoryService.FileSystem.Path.Join(directoryService.PublisherDirectory, publisherFileName);
        if (!directoryService.FileSystem.File.Exists(domainFilePath))
        {
            // We need to request the favicon and save it
            try
            {
                domainFilePath = directoryService.FileSystem.Path.Join(directoryService.PublisherDirectory,
                    await coverDbService.DownloadPublisherImageAsync(publisherName, encodeFormat));
            }
            catch (Exception)
            {
                return BadRequest(await localizationService.TranslateAsync(UserId, "generic-favicon"));
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
    [PersonAccess]
    [HttpGet("person-cover")]
    public async Task<ActionResult> GetPersonCoverImage(int personId, string apiKey)
    {
        var path = Path.Join(directoryService.CoverImageDirectory, await unitOfWork.UserRepository.GetPersonCoverImageAsync(personId));
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
        var filename = await unitOfWork.UserRepository.GetCoverImageAsync(userId);
        if (filename == null) return NotFound();

        var path = Path.Join(directoryService.CoverImageDirectory, filename);
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
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> GetCoverUploadImage(string filename, string apiKey)
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
