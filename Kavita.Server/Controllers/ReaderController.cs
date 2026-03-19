using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Metadata;
using Kavita.Services.Reading;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

/// <summary>
/// For all things regarding reading, mainly focusing on non-Book related entities
/// </summary>
/// <inheritdoc />
public class ReaderController(ICacheService cacheService,
    IUnitOfWork unitOfWork, ILogger<ReaderController> logger,
    IReaderService readerService, IBookmarkService bookmarkService, IEventHub eventHub,
    IScrobblingService scrobblingService,
    ILocalizationService localizationService,
    IBookService bookService) : BaseApiController
{

    /// <summary>
    /// Returns the PDF for the chapterId.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="apiKey">Auth Key for authentication</param>
    /// <param name="extractPdf">Converts PDF into images per-page - Used for Mihon mainly</param>
    /// <returns></returns>
    [ChapterAccess]
    [SkipDeviceTracking]
    [HttpGet("pdf")]
    public async Task<ActionResult> GetPdf(int chapterId, string apiKey, bool extractPdf = false)
    {
        if (!UserContext.IsAuthenticated) return Unauthorized();
        var chapter = await cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        try
        {

            var path = cacheService.GetCachedFile(chapter);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            cacheService.CleanupChapters([chapterId]);
            throw;
        }
    }

    /// <summary>
    /// Returns an image for a given chapter. Will perform bounding checks
    /// </summary>
    /// <remarks>This will cache the chapter images for reading</remarks>
    /// <param name="chapterId">Chapter Id</param>
    /// <param name="page">Page in question</param>
    /// <param name="apiKey">User's API Key for authentication</param>
    /// <param name="extractPdf">Should Kavita extract pdf into images. Defaults to false.</param>
    /// <returns></returns>
    [ChapterAccess]
    [SkipDeviceTracking]
    [HttpGet("image")]
    public async Task<ActionResult> GetImage(int chapterId, int page, string apiKey, bool extractPdf = false)
    {
        if (page < 0) page = 0;

        try
        {
            var chapter = await cacheService.Ensure(chapterId, extractPdf);
            if (chapter == null) return NoContent();

            var path = cacheService.GetCachedPagePath(chapter.Id, page);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            cacheService.CleanupChapters([chapterId]);
            throw;
        }
    }

    /// <summary>
    /// Returns a thumbnail for the given page number
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="pageNum"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [ChapterAccess]
    [SkipDeviceTracking]
    [HttpGet("thumbnail")]
    public async Task<ActionResult> GetThumbnail(int chapterId, int pageNum, string apiKey)
    {
        var chapter = await cacheService.Ensure(chapterId, true);
        if (chapter == null) return NoContent();

        var images = cacheService.GetCachedPages(chapterId);

        var path = await readerService.GetThumbnail(chapter, pageNum, images);
        return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
    }

    /// <summary>
    /// Returns an image for a given bookmark series. Side effect: This will cache the bookmark images for reading.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="apiKey">Api key for the user the bookmarks are on</param>
    /// <param name="page"></param>
    /// <remarks>We must use api key as bookmarks could be leaked to other users via the API</remarks>
    /// <returns></returns>
    [SeriesAccess]
    [SkipDeviceTracking]
    [HttpGet("bookmark-image")]
    public async Task<ActionResult> GetBookmarkImage(int seriesId, string apiKey, int page)
    {
        if (page < 0) page = 0;
        var totalPages = await cacheService.CacheBookmarkForSeries(UserId, seriesId);
        if (page > totalPages)
        {
            page = totalPages;
        }

        try
        {
            var path = cacheService.GetCachedBookmarkPagePath(seriesId, page);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            cacheService.CleanupBookmarks([seriesId]);
            throw;
        }
    }

    /// <summary>
    /// Returns the file dimensions for all pages in a chapter. If the underlying chapter is PDF, use extractPDF to unpack as images.
    /// </summary>
    /// <remarks>This has a side effect of caching the images.
    /// This will only be populated on archive filetypes and not in bookmark mode</remarks>
    /// <param name="chapterId"></param>
    /// <param name="extractPdf"></param>
    /// <returns></returns>
    [ChapterAccess]
    [SkipDeviceTracking]
    [HttpGet("file-dimensions")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "extractPdf"])]
    public async Task<ActionResult<IEnumerable<FileDimensionDto>>> GetFileDimensions(int chapterId, bool extractPdf = false)
    {
        if (chapterId <= 0) return ArraySegment<FileDimensionDto>.Empty;
        var chapter = await cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        return Ok(cacheService.GetCachedFileDimensions(cacheService.GetCachePath(chapterId)));
    }

    /// <summary>
    /// Returns various information about a Chapter. Side effect: This will cache the chapter images for reading.
    /// </summary>
    /// <remarks>This is generally the first call when attempting to read to allow pre-generation of assets needed for reading</remarks>
    /// <param name="chapterId"></param>
    /// <param name="extractPdf">Should Kavita extract pdf into images. Defaults to false.</param>
    /// <param name="includeDimensions">Include file dimensions. Only useful for image-based reading</param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "extractPdf", "includeDimensions"])]
    public async Task<ActionResult<ChapterInfoDto>> GetChapterInfo(int chapterId, bool extractPdf = false, bool includeDimensions = false)
    {
        if (chapterId <= 0) return Ok(null); // This can happen occasionally from UI, we should just ignore
        var chapter = await cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        var dto = await unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await localizationService.Translate(UserId, "perform-scan"));
        var mangaFile = chapter.Files.First();

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(dto.SeriesId, UserId);
        if (series == null) return Unauthorized();

        var info = new ChapterInfoDto()
        {
            ChapterNumber = dto.ChapterNumber,
            VolumeNumber = dto.VolumeNumber,
            VolumeId = dto.VolumeId,
            FileName = Path.GetFileName(mangaFile.FilePath),
            SeriesName = dto.SeriesName,
            SeriesFormat = dto.SeriesFormat,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            IsSpecial = dto.IsSpecial,
            Pages = dto.Pages,
            SeriesTotalPages = series.Pages,
            SeriesTotalPagesRead = series.PagesRead,
            ChapterTitle = dto.ChapterTitle ?? string.Empty,
            Subtitle = string.Empty,
            Title = dto.SeriesName,
        };

        if (includeDimensions)
        {
            info.PageDimensions = cacheService.GetCachedFileDimensions(cacheService.GetCachePath(chapterId));
            info.DoublePairs = readerService.GetPairs(info.PageDimensions);
        }

        if (info.ChapterTitle is {Length: > 0}) {
            // TODO: Can we rework the logic of generating titles for the UI and instead calculate that in the DB?
            info.Title += " - " + info.ChapterTitle;
        }

        if (info.IsSpecial)
        {
            info.Subtitle = Path.GetFileNameWithoutExtension(info.FileName);
        } else if (!info.IsSpecial && Parser.IsLooseLeafVolume(info.VolumeNumber))
        {
            info.Subtitle = ReaderService.FormatChapterName(info.LibraryType, true, true) + info.ChapterNumber;
        }
        else
        {
            info.Subtitle = await localizationService.Translate(UserId, "volume-num", info.VolumeNumber);
            if (!Parser.IsDefaultChapter(info.ChapterNumber))
            {
                info.Subtitle += " " + ReaderService.FormatChapterName(info.LibraryType, true, true) +
                                 info.ChapterNumber;
            }
        }


        return Ok(info);
    }

    /// <summary>
    /// Returns various information about all bookmark files for a Series. Side effect: This will cache the bookmark images for reading.
    /// </summary>
    /// <param name="seriesId">Series Id for all bookmarks</param>
    /// <param name="includeDimensions">Include file dimensions (extra I/O). Defaults to true.</param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("bookmark-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "includeDimensions"])]
    public async Task<ActionResult<BookmarkInfoDto>> GetBookmarkInfo(int seriesId, bool includeDimensions = true)
    {
        var totalPages = await cacheService.CacheBookmarkForSeries(UserId, seriesId);
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.None);

        var info = new BookmarkInfoDto()
        {
            SeriesName = series!.Name,
            SeriesFormat = series.Format,
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            Pages = totalPages,
        };

        if (includeDimensions)
        {
            info.PageDimensions = cacheService.GetCachedFileDimensions(cacheService.GetBookmarkCachePath(seriesId));
            info.DoublePairs = readerService.GetPairs(info.PageDimensions);
        }

        return Ok(info);
    }

    /// <summary>
    /// Mark a single chapter as read
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-chapter-read")]
    public async Task<ActionResult> MarkChapterAsRead(MarkChapterReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Progress, HttpContext.RequestAborted);
        if (user == null) return Unauthorized();

        if (!await unitOfWork.UserRepository.HasAccessToChapter(UserId, dto.ChapterId))
            return NotFound();

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId);
        if (chapter == null) return NotFound();

        var progressDictionary = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersByChapters(UserId, dto.SeriesId, [dto.ChapterId], HttpContext.RequestAborted);

        await readerService.MarkChaptersAsRead(user, dto.SeriesId, [chapter]);

        await unitOfWork.CommitAsync();

        if (dto.GenerateReadingSession)
        {
            BackgroundJob.Enqueue<IReadingSessionService>(s
                => s.GenerateReadingSessionForChapters(UserId, dto.SeriesId, progressDictionary, CancellationToken.None));
        }

        return Ok();
    }


    /// <summary>
    /// Marks a Series as read. All volumes and chapters will be marked as read during this process.
    /// </summary>
    /// <param name="markReadDto"></param>
    /// <returns></returns>
    [HttpPost("mark-read")]
    public async Task<ActionResult> MarkRead(MarkReadDto markReadDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();


        var progressDictionary = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersBySeries(UserId, markReadDto.SeriesId, HttpContext.RequestAborted);

        try
        {
            await readerService.MarkSeriesAsRead(user, markReadDto.SeriesId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, markReadDto.SeriesId));
        BackgroundJob.Enqueue(() => unitOfWork.SeriesRepository.ClearOnDeckRemoval(markReadDto.SeriesId, user.Id));

        if (markReadDto.GenerateReadingSession)
        {
            BackgroundJob.Enqueue<IReadingSessionService>(s
                => s.GenerateReadingSessionForChapters(UserId, markReadDto.SeriesId, progressDictionary, CancellationToken.None));
        }


        return Ok();
    }


    /// <summary>
    /// Marks a Series as Unread. All volumes and chapters will be marked as unread during this process.
    /// </summary>
    /// <param name="markReadDto"></param>
    /// <returns></returns>
    [HttpPost("mark-unread")]
    public async Task<ActionResult> MarkUnread(MarkReadDto markReadDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        await readerService.MarkSeriesAsUnread(user, markReadDto.SeriesId);

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, markReadDto.SeriesId));
        return Ok();
    }

    /// <summary>
    /// Marks all chapters within a volume as unread
    /// </summary>
    /// <param name="markVolumeReadDto"></param>
    /// <returns></returns>
    [HttpPost("mark-volume-unread")]
    public async Task<ActionResult> MarkVolumeAsUnread(MarkVolumeReadDto markVolumeReadDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();

        var chapters = await unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        await readerService.MarkChaptersAsUnread(user, markVolumeReadDto.SeriesId, chapters);

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, markVolumeReadDto.SeriesId));
        return Ok();
    }

    /// <summary>
    /// Marks all chapters within a volume as Read
    /// </summary>
    /// <param name="markVolumeReadDto"></param>
    /// <returns></returns>
    [HttpPost("mark-volume-read")]
    public async Task<ActionResult> MarkVolumeAsRead(MarkVolumeReadDto markVolumeReadDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);

        var chapters = await unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        if (user == null) return Unauthorized();

        var progressDictionary = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersByVolumes(UserId, markVolumeReadDto.SeriesId, [markVolumeReadDto.VolumeId], HttpContext.RequestAborted);

        try
        {
            await readerService.MarkChaptersAsRead(user, markVolumeReadDto.SeriesId, chapters);

        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }


        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
            MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, markVolumeReadDto.SeriesId,
                markVolumeReadDto.VolumeId, 0, chapters.Sum(c => c.Pages)));

        BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, markVolumeReadDto.SeriesId));
        BackgroundJob.Enqueue(() => unitOfWork.SeriesRepository.ClearOnDeckRemoval(markVolumeReadDto.SeriesId, user.Id));

        if (markVolumeReadDto.GenerateReadingSession)
        {
            BackgroundJob.Enqueue<IReadingSessionService>(s
                => s.GenerateReadingSessionForChapters(UserId, markVolumeReadDto.SeriesId, progressDictionary, CancellationToken.None));
        }

        return Ok();
    }


    /// <summary>
    /// Marks all chapters within a list of volumes as Read. All volumes must belong to the same Series.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-read")]
    public async Task<ActionResult> MarkMultipleAsRead(MarkVolumesReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= [];

        var chapterIds = await unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }

        chapterIds = chapterIds.Distinct().ToList();

        var progressDictionary = await unitOfWork.AppUserProgressRepository
            .GetUserProgressForChaptersByChapters(UserId, dto.SeriesId, chapterIds.ToList(), HttpContext.RequestAborted);

        var chapters = await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
        await readerService.MarkChaptersAsRead(user, dto.SeriesId, chapters.ToList());

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, dto.SeriesId));
        BackgroundJob.Enqueue(() => unitOfWork.SeriesRepository.ClearOnDeckRemoval(dto.SeriesId, user.Id));

        if (dto.GenerateReadingSession)
        {
            BackgroundJob.Enqueue<IReadingSessionService>(s
                => s.GenerateReadingSessionForChapters(UserId, dto.SeriesId, progressDictionary, CancellationToken.None));
        }

        return Ok();
    }

    /// <summary>
    /// Marks all chapters within a list of volumes as Unread. All volumes must belong to the same Series.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-unread")]
    public async Task<ActionResult> MarkMultipleAsUnread(MarkVolumesReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var chapterIds = await unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }
        var chapters = await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
        await readerService.MarkChaptersAsUnread(user, dto.SeriesId, chapters.ToList());

        if (await unitOfWork.CommitAsync())
        {
            BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, dto.SeriesId));
            return Ok();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));
    }

    /// <summary>
    /// Marks all chapters within a list of series as Read.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-series-read")]
    public async Task<ActionResult> MarkMultipleSeriesAsRead(MarkMultipleSeriesAsReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
        foreach (var volume in volumes)
        {
            await readerService.MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));

        foreach (var sId in dto.SeriesIds)
        {
            BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, sId));
            BackgroundJob.Enqueue(() => unitOfWork.SeriesRepository.ClearOnDeckRemoval(sId, user.Id));

            var progressDictionary = await unitOfWork.AppUserProgressRepository
                .GetUserProgressForChaptersBySeries(UserId, sId, HttpContext.RequestAborted);

            BackgroundJob.Enqueue<IReadingSessionService>(s
                => s.GenerateReadingSessionForChapters(UserId, sId, progressDictionary, CancellationToken.None));
        }
        return Ok();
    }

    /// <summary>
    /// Marks all chapters within a list of series as Unread.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-series-unread")]
    public async Task<ActionResult> MarkMultipleSeriesAsUnread(MarkMultipleSeriesAsReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= [];

        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
        foreach (var volume in volumes)
        {
            await readerService.MarkChaptersAsUnread(user, volume.SeriesId, volume.Chapters);
        }

        if (await unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, sId));
            }
            return Ok();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-read-progress"));
    }

    /// <summary>
    /// Returns Progress (page number) for a chapter for the logged in user
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("get-progress")]
    public async Task<ActionResult<ProgressDto>> GetProgress(int chapterId)
    {
        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, UserId);
        logger.LogDebug("Get Progress for {ChapterId} is {Pages}", chapterId, progress?.PageNum ?? 0);

        if (progress == null) return Ok(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = chapterId,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0
        });
        return Ok(progress);
    }

    /// <summary>
    /// Save page against Chapter for authenticated user
    /// </summary>
    /// <param name="progressDto"></param>
    /// <returns></returns>
    [HttpPost("progress")]
    public async Task<ActionResult> SaveProgress(ProgressDto progressDto)
    {
        var userId = UserId;

        if (!await readerService.SaveReadingProgress(progressDto, userId))
        {
            return BadRequest(await localizationService.Translate(userId, "generic-read-progress"));
        }

        return Ok();
    }

    /// <summary>
    /// Continue point is the chapter which you should start reading again from. If there is no progress on a series, then the first chapter will be returned (non-special unless only specials).
    /// Otherwise, loop through the chapters and volumes in order to find the next chapter which has progress.
    /// </summary>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("continue-point")]
    public async Task<ActionResult<ChapterDto>> GetContinuePoint(int seriesId)
    {
        return Ok(await readerService.GetContinuePoint(seriesId, UserId));
    }

    /// <summary>
    /// Returns if the user has reading progress on the Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("has-progress")]
    public async Task<ActionResult<bool>> HasProgress(int seriesId)
    {
        return Ok(await unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(seriesId, UserId));
    }

    /// <summary>
    /// Returns a list of bookmarked pages for a given Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarks(int chapterId)
    {
        return Ok(await unitOfWork.UserRepository.GetBookmarkDtosForChapter(UserId, chapterId));
    }

    /// <summary>
    /// Returns a list of all bookmarked pages for a User
    /// </summary>
    /// <param name="filterDto">Only supports SeriesNameQuery</param>
    /// <returns></returns>
    [HttpPost("all-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetAllBookmarks(FilterV2Dto filterDto)
    {
        return Ok(await unitOfWork.UserRepository.GetAllBookmarkDtos(UserId, filterDto));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("remove-bookmarks")]
    public async Task<ActionResult> RemoveBookmarks(RemoveBookmarkForSeriesDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok(await localizationService.Translate(UserId, "nothing-to-do"));

        try
        {
            var bookmarksToRemove = user.Bookmarks.Where(bmk => bmk.SeriesId == dto.SeriesId).ToList();
            user.Bookmarks = user.Bookmarks.Where(bmk => bmk.SeriesId != dto.SeriesId).ToList();
            unitOfWork.UserRepository.Update(user);

            if (!unitOfWork.HasChanges() || await unitOfWork.CommitAsync())
            {
                try
                {
                    await bookmarkService.DeleteBookmarkFiles(bookmarksToRemove);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "There was an issue cleaning up old bookmarks");
                }
                return Ok();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when trying to clear bookmarks");
            await unitOfWork.RollbackAsync();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("bulk-remove-bookmarks")]
    public async Task<ActionResult> BulkRemoveBookmarks(BulkRemoveBookmarkForSeriesDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok(await localizationService.Translate(UserId, "nothing-to-do"));

        try
        {
            foreach (var seriesId in dto.SeriesIds)
            {
                var bookmarksToRemove = user.Bookmarks.Where(bmk => bmk.SeriesId == seriesId).ToList();
                user.Bookmarks = user.Bookmarks.Where(bmk => bmk.SeriesId != seriesId).ToList();
                unitOfWork.UserRepository.Update(user);
                await bookmarkService.DeleteBookmarkFiles(bookmarksToRemove);
            }


            if (!unitOfWork.HasChanges() || await unitOfWork.CommitAsync())
            {
                return Ok();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when trying to clear bookmarks");
            await unitOfWork.RollbackAsync();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForVolume(int volumeId)
    {
        return Ok(await unitOfWork.UserRepository.GetBookmarkDtosForVolume(UserId, volumeId));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("series-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForSeries(int seriesId)
    {
        return Ok(await unitOfWork.UserRepository.GetBookmarkDtosForSeries(UserId, seriesId));
    }

    /// <summary>
    /// Bookmarks a page against a Chapter
    /// </summary>
    /// <remarks>This has a side effect of caching the chapter files to disk</remarks>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("bookmark")]
    [Authorize(PolicyGroups.BookmarkPolicy)]
    public async Task<ActionResult> BookmarkPage(BookmarkDto bookmarkDto)
    {
        try
        {
            // Don't let user save past total pages.
            var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
            if (user == null) return new UnauthorizedResult();

            var chapter = await cacheService.Ensure(bookmarkDto.ChapterId);
            if (chapter == null || chapter.Files.Count == 0)
                return BadRequest(await localizationService.Translate(UserId, "cache-file-find"));

            bookmarkDto.Page = readerService.CapPageToChapter(chapter, bookmarkDto.Page);


            string path;
            string? chapterTitle;
            if (Parser.IsEpub(chapter.Files.First().Extension!))
            {
                var cachedFilePath = cacheService.GetCachedFile(chapter);
                path = await bookService.CopyImageToTempFromBook(chapter.Id, bookmarkDto, cachedFilePath);


                var chapterEntity =  await unitOfWork.ChapterRepository.GetChapterAsync(bookmarkDto.ChapterId);
                if (chapterEntity == null) return BadRequest(await localizationService.Translate(UserId, "chapter-doesnt-exist"));
                var toc = await bookService.GenerateTableOfContents(chapterEntity);
                chapterTitle = BookService.GetChapterTitleFromToC(toc, bookmarkDto.Page);
            }
            else
            {
                path = cacheService.GetCachedPagePath(chapter.Id, bookmarkDto.Page);
                chapterTitle = chapter.TitleName;
            }

            bookmarkDto.ChapterTitle = chapterTitle;



            if (string.IsNullOrEmpty(path) || !await bookmarkService.BookmarkPage(user, bookmarkDto, path))
            {
                return BadRequest(await localizationService.Translate(UserId, "bookmark-save"));
            }


            BackgroundJob.Enqueue(() => cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));

            return Ok();
        }
        catch (KavitaException ex)
        {
            logger.LogError(ex, "There was an exception when trying to create a bookmark");
            return BadRequest(await localizationService.Translate(UserId, "bookmark-save"));
        }
    }


    /// <summary>
    /// Removes a bookmarked page for a Chapter
    /// </summary>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("unbookmark")]
    [Authorize(PolicyGroups.BookmarkPolicy)]
    public async Task<ActionResult> UnBookmarkPage(BookmarkDto bookmarkDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return new UnauthorizedResult();

        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok();

        if (!await bookmarkService.RemoveBookmarkPage(user, bookmarkDto))
        {
            return BadRequest(await localizationService.Translate(UserId, "bookmark-save"));
        }


        BackgroundJob.Enqueue(() => cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));

        return Ok();
    }

    /// <summary>
    /// Returns the next logical chapter from the series.
    /// </summary>
    /// <example>
    /// V1 → V2 → V3 chapter 0 → V3 chapter 10 → SP 01 → SP 02
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <returns>chapter id for next manga</returns>
    [SeriesAccess]
    [HttpGet("next-chapter")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "volumeId", "currentChapterId"])]
    public async Task<ActionResult<int>> GetNextChapter(int seriesId, int volumeId, int currentChapterId)
    {
        return Ok(await readerService.GetNextChapterIdAsync(seriesId, volumeId, currentChapterId, UserId));
    }


    /// <summary>
    /// Returns the previous logical chapter from the series.
    /// </summary>
    /// <example>
    /// V1 ← V2 ← V3 chapter 0 ← V3 chapter 10 ← SP 01 ← SP 02
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <returns>chapter id for next manga</returns>
    [SeriesAccess]
    [HttpGet("prev-chapter")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "volumeId", "currentChapterId"])]
    public async Task<ActionResult<int>> GetPreviousChapter(int seriesId, int volumeId, int currentChapterId)
    {
        return Ok(await readerService.GetPrevChapterIdAsync(seriesId, volumeId, currentChapterId, UserId));
    }

    /// <summary>
    /// For the current user, returns an estimate on how long it would take to finish reading the series.
    /// </summary>
    /// <remarks>For Epubs, this does not check words inside a chapter due to overhead so may not work in all cases.</remarks>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("time-left")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId"])]
    public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletion(int seriesId)
    {
        var userId = UserId;
        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        if (series == null) return BadRequest(await localizationService.Translate(UserId, "series-doesnt-exist"));

        // Get all sum of all chapters with progress that is complete then subtract from series. Multiply by modifiers
        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(seriesId, userId);
        if (series.Format == MangaFormat.Epub)
        {
            var chapters =
                await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(progress.Select(p => p.ChapterId).ToList());
            // Word count
            var progressCount = chapters.Sum(c => c.WordCount);
            var wordsLeft = series.WordCount - progressCount;
            return ReaderService.GetTimeEstimate(wordsLeft, 0, true);
        }

        var progressPageCount = progress.Sum(p => p.PagesRead);
        var pagesLeft = series.Pages - progressPageCount;

        return Ok(ReaderService.GetTimeEstimate(0, pagesLeft, false));
    }


    /// <summary>
    /// For the current user, returns an estimate on how long it would take to finish reading the chapter.
    /// </summary>
    /// <remarks>For Epubs, this does not check words inside a chapter due to overhead so may not work in all cases.</remarks>
    /// <param name="seriesId"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("time-left-for-chapter")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "chapterId"])]
    public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletionForChapter(int seriesId, int chapterId)
    {
        return Ok(await readerService.GetEstimateToCompletionForChapter(UserId, seriesId, chapterId));
    }



    /// <summary>
    /// Returns the user's personal table of contents for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("ptoc")]
    public ActionResult<IEnumerable<PersonalToCDto>> GetPersonalToC(int chapterId)
    {
        return Ok(unitOfWork.UserTableOfContentRepository.GetPersonalToC(UserId, chapterId));
    }

    /// <summary>
    /// Deletes the user's personal table of content for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="pageNum"></param>
    /// <param name="title"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpDelete("ptoc")]
    public async Task<ActionResult> DeletePersonalToc([FromQuery] int chapterId, [FromQuery] int pageNum, [FromQuery] string title)
    {
        var userId = UserId;
        if (string.IsNullOrWhiteSpace(title)) return BadRequest(await localizationService.Translate(userId, "name-required"));
        if (pageNum < 0) return BadRequest(await localizationService.Translate(userId, "valid-number"));

        var toc = await unitOfWork.UserTableOfContentRepository.Get(userId, chapterId, pageNum, title);
        if (toc == null) return Ok();

        unitOfWork.UserTableOfContentRepository.Remove(toc);
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Create a new personal table of content entry for a given chapter
    /// </summary>
    /// <remarks>The title and page number must be unique to that book</remarks>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create-ptoc")]
    public async Task<ActionResult> CreatePersonalToC(CreatePersonalToCDto dto)
    {
        // Validate there isn't already an existing page title combo?
        var userId = UserId;
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(await localizationService.Translate(userId, "name-required"));

        if (!await unitOfWork.UserRepository.HasAccessToChapter(UserId, dto.ChapterId)) return NotFound();

        if (dto.PageNumber < 0) return BadRequest(await localizationService.Translate(userId, "valid-number"));
        if (await unitOfWork.UserTableOfContentRepository.IsUnique(userId, dto.ChapterId, dto.PageNumber,
                dto.Title.Trim()))
        {
            return BadRequest(await localizationService.Translate(userId, "duplicate-bookmark"));
        }

        // Look up the chapter this PTOC is associated with to get the chapter title (if there is one)
        var chapter =  await unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId);
        if (chapter == null) return BadRequest(await localizationService.Translate(userId, "chapter-doesnt-exist"));
        var toc = await bookService.GenerateTableOfContents(chapter);
        var chapterTitle = BookService.GetChapterTitleFromToC(toc, dto.PageNumber);

        unitOfWork.UserTableOfContentRepository.Attach(new AppUserTableOfContent()
        {
            Title = dto.Title.Trim(),
            ChapterId = dto.ChapterId,
            PageNumber = dto.PageNumber,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            BookScrollId = dto.BookScrollId,
            SelectedText = dto.SelectedText,
            ChapterTitle = chapterTitle,
            AppUserId = userId
        });
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("prompt-reread/series")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForSeriesReRead(int seriesId, int libraryId)
    {
        return Ok(await readerService.CheckSeriesForReRead(UserId, seriesId, libraryId));
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given volume
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("prompt-reread/volume")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForVolumeReRead(int libraryId, int seriesId, int volumeId)
    {
        return Ok(await readerService.CheckVolumeForReRead(UserId, volumeId, seriesId, libraryId));
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given chapter
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("prompt-reread/chapter")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForChapterReRead(int libraryId, int seriesId, int chapterId)
    {
        return Ok(await readerService.CheckChapterForReRead(UserId, chapterId, seriesId, libraryId));
    }

    [HttpGet("first-progress-date")]
    public async Task<ActionResult<DateTime>> GetFirstReadingDate(int userId)
    {
        return Ok(await unitOfWork.AppUserProgressRepository.GetFirstProgressForUser(userId));
    }

}
