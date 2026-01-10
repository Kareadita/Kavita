using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering.v2;
using API.DTOs.Progress;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Middleware;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeTypes;

namespace API.Controllers;

#nullable enable

/// <summary>
/// For all things regarding reading, mainly focusing on non-Book related entities
/// </summary>
public class ReaderController : BaseApiController
{
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReaderController> _logger;
    private readonly IReaderService _readerService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IAccountService _accountService;
    private readonly IEventHub _eventHub;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILocalizationService _localizationService;
    private readonly IBookService _bookService;

    /// <inheritdoc />
    public ReaderController(ICacheService cacheService,
        IUnitOfWork unitOfWork, ILogger<ReaderController> logger,
        IReaderService readerService, IBookmarkService bookmarkService,
        IAccountService accountService, IEventHub eventHub,
        IScrobblingService scrobblingService,
        ILocalizationService localizationService,
        IBookService bookService)
    {
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _readerService = readerService;
        _bookmarkService = bookmarkService;
        _accountService = accountService;
        _eventHub = eventHub;
        _scrobblingService = scrobblingService;
        _localizationService = localizationService;
        _bookService = bookService;
    }

    /// <summary>
    /// Returns the PDF for the chapterId.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="apiKey">Auth Key for authentication</param>
    /// <param name="extractPdf">Converts PDF into images per-page - Used for Mihon mainly</param>
    /// <returns></returns>
    [HttpGet("pdf")]
    [SkipDeviceTracking]
    public async Task<ActionResult> GetPdf(int chapterId, string apiKey, bool extractPdf = false)
    {
        if (!UserContext.IsAuthenticated) return Unauthorized();
        var chapter = await _cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        // Validate the user has access to the PDF
        var series = await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapter.Id,
            await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(Username!));
        if (series == null) return BadRequest(await _localizationService.Translate(UserId, "invalid-access"));

        try
        {

            var path = _cacheService.GetCachedFile(chapter);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            _cacheService.CleanupChapters([chapterId]);
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
    [HttpGet("image")]
    [SkipDeviceTracking]
    [AllowAnonymous]
    public async Task<ActionResult> GetImage(int chapterId, int page, string apiKey, bool extractPdf = false)
    {
        if (page < 0) page = 0;

        try
        {
            var chapter = await _cacheService.Ensure(chapterId, extractPdf);
            if (chapter == null) return NoContent();

            var path = _cacheService.GetCachedPagePath(chapter.Id, page);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            _cacheService.CleanupChapters([chapterId]);
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
    [HttpGet("thumbnail")]
    [SkipDeviceTracking]
    [AllowAnonymous]
    public async Task<ActionResult> GetThumbnail(int chapterId, int pageNum, string apiKey)
    {
        var chapter = await _cacheService.Ensure(chapterId, true);
        if (chapter == null) return NoContent();
        var images = _cacheService.GetCachedPages(chapterId);

        var path = await _readerService.GetThumbnail(chapter, pageNum, images);
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
    [HttpGet("bookmark-image")]
    [SkipDeviceTracking]
    [AllowAnonymous]
    public async Task<ActionResult> GetBookmarkImage(int seriesId, string apiKey, int page)
    {
        if (page < 0) page = 0;
        var totalPages = await _cacheService.CacheBookmarkForSeries(UserId, seriesId);
        if (page > totalPages)
        {
            page = totalPages;
        }

        try
        {
            var path = _cacheService.GetCachedBookmarkPagePath(seriesId, page);
            return CachedFile(path, maxAge: TimeSpan.FromHours(1).Seconds);
        }
        catch (Exception)
        {
            _cacheService.CleanupBookmarks([seriesId]);
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
    [HttpGet("file-dimensions")]
    [SkipDeviceTracking]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "extractPdf"])]
    public async Task<ActionResult<IEnumerable<FileDimensionDto>>> GetFileDimensions(int chapterId, bool extractPdf = false)
    {
        if (chapterId <= 0) return ArraySegment<FileDimensionDto>.Empty;
        var chapter = await _cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        return Ok(_cacheService.GetCachedFileDimensions(_cacheService.GetCachePath(chapterId)));
    }

    /// <summary>
    /// Returns various information about a Chapter. Side effect: This will cache the chapter images for reading.
    /// </summary>
    /// <remarks>This is generally the first call when attempting to read to allow pre-generation of assets needed for reading</remarks>
    /// <param name="chapterId"></param>
    /// <param name="extractPdf">Should Kavita extract pdf into images. Defaults to false.</param>
    /// <param name="includeDimensions">Include file dimensions. Only useful for image-based reading</param>
    /// <returns></returns>
    [HttpGet("chapter-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "extractPdf", "includeDimensions"])]
    public async Task<ActionResult<ChapterInfoDto>> GetChapterInfo(int chapterId, bool extractPdf = false, bool includeDimensions = false)
    {
        if (chapterId <= 0) return Ok(null); // This can happen occasionally from UI, we should just ignore
        var chapter = await _cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        var dto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await _localizationService.Translate(UserId, "perform-scan"));
        var mangaFile = chapter.Files.First();

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(dto.SeriesId, UserId);
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
            info.PageDimensions = _cacheService.GetCachedFileDimensions(_cacheService.GetCachePath(chapterId));
            info.DoublePairs = _readerService.GetPairs(info.PageDimensions);
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
            info.Subtitle = await _localizationService.Translate(UserId, "volume-num", info.VolumeNumber);
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
    [HttpGet("bookmark-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "includeDimensions"])]
    public async Task<ActionResult<BookmarkInfoDto>> GetBookmarkInfo(int seriesId, bool includeDimensions = true)
    {
        var totalPages = await _cacheService.CacheBookmarkForSeries(UserId, seriesId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.None);

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
            info.PageDimensions = _cacheService.GetCachedFileDimensions(_cacheService.GetBookmarkCachePath(seriesId));
            info.DoublePairs = _readerService.GetPairs(info.PageDimensions);
        }

        return Ok(info);
    }


    /// <summary>
    /// Marks a Series as read. All volumes and chapters will be marked as read during this process.
    /// </summary>
    /// <param name="markReadDto"></param>
    /// <returns></returns>
    [HttpPost("mark-read")]
    public async Task<ActionResult> MarkRead(MarkReadDto markReadDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        try
        {
            await _readerService.MarkSeriesAsRead(user, markReadDto.SeriesId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, markReadDto.SeriesId));
        BackgroundJob.Enqueue(() => _unitOfWork.SeriesRepository.ClearOnDeckRemoval(markReadDto.SeriesId, user.Id));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        await _readerService.MarkSeriesAsUnread(user, markReadDto.SeriesId);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, markReadDto.SeriesId));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        await _readerService.MarkChaptersAsUnread(user, markVolumeReadDto.SeriesId, chapters);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, markVolumeReadDto.SeriesId));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        if (user == null) return Unauthorized();
        try
        {
            await _readerService.MarkChaptersAsRead(user, markVolumeReadDto.SeriesId, chapters);

        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }


        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
            MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, markVolumeReadDto.SeriesId,
                markVolumeReadDto.VolumeId, 0, chapters.Sum(c => c.Pages)));

        BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, markVolumeReadDto.SeriesId));
        BackgroundJob.Enqueue(() => _unitOfWork.SeriesRepository.ClearOnDeckRemoval(markVolumeReadDto.SeriesId, user.Id));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= [];

        var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
        await _readerService.MarkChaptersAsRead(user, dto.SeriesId, chapters.ToList());

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, dto.SeriesId));
        BackgroundJob.Enqueue(() => _unitOfWork.SeriesRepository.ClearOnDeckRemoval(dto.SeriesId, user.Id));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }
        var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
        await _readerService.MarkChaptersAsUnread(user, dto.SeriesId, chapters.ToList());

        if (await _unitOfWork.CommitAsync())
        {
            BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, dto.SeriesId));
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));
    }

    /// <summary>
    /// Marks all chapters within a list of series as Read.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-series-read")]
    public async Task<ActionResult> MarkMultipleSeriesAsRead(MarkMultipleSeriesAsReadDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
        foreach (var volume in volumes)
        {
            await _readerService.MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));

        foreach (var sId in dto.SeriesIds)
        {
            BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, sId));
            BackgroundJob.Enqueue(() => _unitOfWork.SeriesRepository.ClearOnDeckRemoval(sId, user.Id));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= [];

        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
        foreach (var volume in volumes)
        {
            await _readerService.MarkChaptersAsUnread(user, volume.SeriesId, volume.Chapters);
        }

        if (await _unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, sId));
            }
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-read-progress"));
    }

    /// <summary>
    /// Returns Progress (page number) for a chapter for the logged in user
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("get-progress")]
    public async Task<ActionResult<ProgressDto>> GetProgress(int chapterId)
    {
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, UserId);
        _logger.LogDebug("Get Progress for {ChapterId} is {Pages}", chapterId, progress?.PageNum ?? 0);

        if (progress == null) return Ok(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = chapterId,
            VolumeId = 0,
            SeriesId = 0
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

        if (!await _readerService.SaveReadingProgress(progressDto, userId))
        {
            return BadRequest(await _localizationService.Translate(userId, "generic-read-progress"));
        }

        return Ok();
    }

    /// <summary>
    /// Continue point is the chapter which you should start reading again from. If there is no progress on a series, then the first chapter will be returned (non-special unless only specials).
    /// Otherwise, loop through the chapters and volumes in order to find the next chapter which has progress.
    /// </summary>
    /// <returns></returns>
    [HttpGet("continue-point")]
    public async Task<ActionResult<ChapterDto>> GetContinuePoint(int seriesId)
    {
        return Ok(await _readerService.GetContinuePoint(seriesId, UserId));
    }

    /// <summary>
    /// Returns if the user has reading progress on the Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("has-progress")]
    public async Task<ActionResult<bool>> HasProgress(int seriesId)
    {
        return Ok(await _unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(seriesId, UserId));
    }

    /// <summary>
    /// Returns a list of bookmarked pages for a given Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarks(int chapterId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForChapter(UserId, chapterId));
    }

    /// <summary>
    /// Returns a list of all bookmarked pages for a User
    /// </summary>
    /// <param name="filterDto">Only supports SeriesNameQuery</param>
    /// <returns></returns>
    [HttpPost("all-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetAllBookmarks(FilterV2Dto filterDto)
    {
        return Ok(await _unitOfWork.UserRepository.GetAllBookmarkDtos(UserId, filterDto));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("remove-bookmarks")]
    public async Task<ActionResult> RemoveBookmarks(RemoveBookmarkForSeriesDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));

        try
        {
            var bookmarksToRemove = user.Bookmarks.Where(bmk => bmk.SeriesId == dto.SeriesId).ToList();
            user.Bookmarks = user.Bookmarks.Where(bmk => bmk.SeriesId != dto.SeriesId).ToList();
            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                try
                {
                    await _bookmarkService.DeleteBookmarkFiles(bookmarksToRemove);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was an issue cleaning up old bookmarks");
                }
                return Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when trying to clear bookmarks");
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("bulk-remove-bookmarks")]
    public async Task<ActionResult> BulkRemoveBookmarks(BulkRemoveBookmarkForSeriesDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));

        try
        {
            foreach (var seriesId in dto.SeriesIds)
            {
                var bookmarksToRemove = user.Bookmarks.Where(bmk => bmk.SeriesId == seriesId).ToList();
                user.Bookmarks = user.Bookmarks.Where(bmk => bmk.SeriesId != seriesId).ToList();
                _unitOfWork.UserRepository.Update(user);
                await _bookmarkService.DeleteBookmarkFiles(bookmarksToRemove);
            }


            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                return Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when trying to clear bookmarks");
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(UserId, "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForVolume(int volumeId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForVolume(UserId, volumeId));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("series-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForSeries(int seriesId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForSeries(UserId, seriesId));
    }

    /// <summary>
    /// Bookmarks a page against a Chapter
    /// </summary>
    /// <remarks>This has a side effect of caching the chapter files to disk</remarks>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("bookmark")]
    public async Task<ActionResult> BookmarkPage(BookmarkDto bookmarkDto)
    {
        try
        {
            // Don't let user save past total pages.
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
                AppUserIncludes.Bookmarks);
            if (user == null) return new UnauthorizedResult();

            if (!await _accountService.HasBookmarkPermission(user))
                return BadRequest(await _localizationService.Translate(UserId, "bookmark-permission"));

            var chapter = await _cacheService.Ensure(bookmarkDto.ChapterId);
            if (chapter == null || chapter.Files.Count == 0)
                return BadRequest(await _localizationService.Translate(UserId, "cache-file-find"));

            bookmarkDto.Page = _readerService.CapPageToChapter(chapter, bookmarkDto.Page);


            string path;
            string? chapterTitle;
            if (Parser.IsEpub(chapter.Files.First().Extension!))
            {
                var cachedFilePath = _cacheService.GetCachedFile(chapter);
                path = await _bookService.CopyImageToTempFromBook(chapter.Id, bookmarkDto, cachedFilePath);


                var chapterEntity =  await _unitOfWork.ChapterRepository.GetChapterAsync(bookmarkDto.ChapterId);
                if (chapterEntity == null) return BadRequest(await _localizationService.Translate(UserId, "chapter-doesnt-exist"));
                var toc = await _bookService.GenerateTableOfContents(chapterEntity);
                chapterTitle = BookService.GetChapterTitleFromToC(toc, bookmarkDto.Page);
            }
            else
            {
                path = _cacheService.GetCachedPagePath(chapter.Id, bookmarkDto.Page);
                chapterTitle = chapter.TitleName;
            }

            bookmarkDto.ChapterTitle = chapterTitle;



            if (string.IsNullOrEmpty(path) || !await _bookmarkService.BookmarkPage(user, bookmarkDto, path))
            {
                return BadRequest(await _localizationService.Translate(UserId, "bookmark-save"));
            }


            BackgroundJob.Enqueue(() => _cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));

            return Ok();
        }
        catch (KavitaException ex)
        {
            _logger.LogError(ex, "There was an exception when trying to create a bookmark");
            return BadRequest(await _localizationService.Translate(UserId, "bookmark-save"));
        }
    }


    /// <summary>
    /// Removes a bookmarked page for a Chapter
    /// </summary>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("unbookmark")]
    public async Task<ActionResult> UnBookmarkPage(BookmarkDto bookmarkDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Bookmarks);
        if (user == null) return new UnauthorizedResult();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok();

        if (!await _accountService.HasBookmarkPermission(user))
        {
            return BadRequest(await _localizationService.Translate(UserId, "bookmark-permission"));
        }

        if (!await _bookmarkService.RemoveBookmarkPage(user, bookmarkDto))
        {
            return BadRequest(await _localizationService.Translate(UserId, "bookmark-save"));
        }


        BackgroundJob.Enqueue(() => _cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));

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
    [ResponseCache(CacheProfileName = "Hour", VaryByQueryKeys = ["seriesId", "volumeId", "currentChapterId"])]
    [HttpGet("next-chapter")]
    public async Task<ActionResult<int>> GetNextChapter(int seriesId, int volumeId, int currentChapterId)
    {
        return Ok(await _readerService.GetNextChapterIdAsync(seriesId, volumeId, currentChapterId, UserId));
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
    [ResponseCache(CacheProfileName = "Hour", VaryByQueryKeys = ["seriesId", "volumeId", "currentChapterId"])]
    [HttpGet("prev-chapter")]
    public async Task<ActionResult<int>> GetPreviousChapter(int seriesId, int volumeId, int currentChapterId)
    {
        return Ok(await _readerService.GetPrevChapterIdAsync(seriesId, volumeId, currentChapterId, UserId));
    }

    /// <summary>
    /// For the current user, returns an estimate on how long it would take to finish reading the series.
    /// </summary>
    /// <remarks>For Epubs, this does not check words inside a chapter due to overhead so may not work in all cases.</remarks>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("time-left")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId"])]
    public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletion(int seriesId)
    {
        var userId = UserId;
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        if (series == null) return BadRequest(await _localizationService.Translate(UserId, "series-doesnt-exist"));

        // Get all sum of all chapters with progress that is complete then subtract from series. Multiply by modifiers
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(seriesId, userId);
        if (series.Format == MangaFormat.Epub)
        {
            var chapters =
                await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(progress.Select(p => p.ChapterId).ToList());
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
    [HttpGet("time-left-for-chapter")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "chapterId"])]
    public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletionForChapter(int seriesId, int chapterId)
    {
        var userId = UserId;
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId);
        if (series == null || chapter == null) return BadRequest(await _localizationService.Translate(UserId, "generic-error"));

        if (series.Format == MangaFormat.Epub)
        {
            // Get the word counts for all the pages
            var pageCounts = await _bookService.GetWordCountsPerPage(chapter.Files.First().FilePath); // TODO: Cache
            if (pageCounts == null) return ReaderService.GetTimeEstimate(series.WordCount, 0, true);

            // Sum character counts only for pages that have been read
            var totalCharactersRead = pageCounts
                .Where(kvp => kvp.Key <= chapter.PagesRead)
                .Sum(kvp => kvp.Value);

            var progressCount = WordCountAnalyzerService.GetWordCount(totalCharactersRead);
            var wordsLeft = series.WordCount - progressCount;
            return ReaderService.GetTimeEstimate(wordsLeft, 0, true);
        }

        var pagesLeft = chapter.Pages - chapter.PagesRead;

        return Ok(ReaderService.GetTimeEstimate(0, pagesLeft, false));
    }



    /// <summary>
    /// Returns the user's personal table of contents for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("ptoc")]
    public ActionResult<IEnumerable<PersonalToCDto>> GetPersonalToC(int chapterId)
    {
        return Ok(_unitOfWork.UserTableOfContentRepository.GetPersonalToC(UserId, chapterId));
    }

    /// <summary>
    /// Deletes the user's personal table of content for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="pageNum"></param>
    /// <param name="title"></param>
    /// <returns></returns>
    [HttpDelete("ptoc")]
    public async Task<ActionResult> DeletePersonalToc([FromQuery] int chapterId, [FromQuery] int pageNum, [FromQuery] string title)
    {
        var userId = UserId;
        if (string.IsNullOrWhiteSpace(title)) return BadRequest(await _localizationService.Translate(userId, "name-required"));
        if (pageNum < 0) return BadRequest(await _localizationService.Translate(userId, "valid-number"));

        var toc = await _unitOfWork.UserTableOfContentRepository.Get(userId, chapterId, pageNum, title);
        if (toc == null) return Ok();

        _unitOfWork.UserTableOfContentRepository.Remove(toc);
        await _unitOfWork.CommitAsync();

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
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(await _localizationService.Translate(userId, "name-required"));
        if (dto.PageNumber < 0) return BadRequest(await _localizationService.Translate(userId, "valid-number"));
        if (await _unitOfWork.UserTableOfContentRepository.IsUnique(userId, dto.ChapterId, dto.PageNumber,
                dto.Title.Trim()))
        {
            return BadRequest(await _localizationService.Translate(userId, "duplicate-bookmark"));
        }

        // Look up the chapter this PTOC is associated with to get the chapter title (if there is one)
        var chapter =  await _unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(userId, "chapter-doesnt-exist"));
        var toc = await _bookService.GenerateTableOfContents(chapter);
        var chapterTitle = BookService.GetChapterTitleFromToC(toc, dto.PageNumber);

        _unitOfWork.UserTableOfContentRepository.Attach(new AppUserTableOfContent()
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
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("prompt-reread/series")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForSeriesReRead(int seriesId, int libraryId)
    {
        return Ok(await _readerService.CheckSeriesForReRead(UserId, seriesId, libraryId));
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given volume
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("prompt-reread/volume")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForVolumeReRead(int libraryId, int seriesId, int volumeId)
    {
        return Ok(await _readerService.CheckVolumeForReRead(UserId, volumeId, seriesId, libraryId));
    }

    /// <summary>
    /// Check if we should prompt the user for rereads for the given chapter
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("prompt-reread/chapter")]
    public async Task<ActionResult<RereadDto>> ShouldPromptForChapterReRead(int libraryId, int seriesId, int chapterId)
    {
        return Ok(await _readerService.CheckChapterForReRead(UserId, chapterId, seriesId, libraryId));
    }

    [HttpGet("first-progress-date")]
    public async Task<ActionResult<DateTime>> GetFirstReadingDate(int userId)
    {
        return Ok(await _unitOfWork.AppUserProgressRepository.GetFirstProgressForUser(userId));
    }

}
