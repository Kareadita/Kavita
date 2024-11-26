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
using API.Extensions;
using API.Services;
using API.Services.Plus;
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

    /// <inheritdoc />
    public ReaderController(ICacheService cacheService,
        IUnitOfWork unitOfWork, ILogger<ReaderController> logger,
        IReaderService readerService, IBookmarkService bookmarkService,
        IAccountService accountService, IEventHub eventHub,
        IScrobblingService scrobblingService,
        ILocalizationService localizationService)
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
    }

    /// <summary>
    /// Returns the PDF for the chapterId.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("pdf")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "apiKey"])]
    public async Task<ActionResult> GetPdf(int chapterId, string apiKey, bool extractPdf = false)
    {
        if (await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey) == 0) return BadRequest();
        var chapter = await _cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        // Validate the user has access to the PDF
        var series = await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapter.Id,
            await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername()));
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "invalid-access"));

        try
        {

            var path = _cacheService.GetCachedFile(chapter);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "pdf-doesnt-exist"));

            return PhysicalFile(path, MimeTypeMap.GetMimeType(Path.GetExtension(path)), Path.GetFileName(path), true);
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "page", "extractPdf", "apiKey"
    ])]
    [AllowAnonymous]
    public async Task<ActionResult> GetImage(int chapterId, int page, string apiKey, bool extractPdf = false)
    {
        if (page < 0) page = 0;
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return BadRequest();

        try
        {
            var chapter = await _cacheService.Ensure(chapterId, extractPdf);
            if (chapter == null) return NoContent();

            var path = _cacheService.GetCachedPagePath(chapter.Id, page);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return BadRequest(await _localizationService.Translate(userId, "no-image-for-page", page));
            var format = Path.GetExtension(path);

            return PhysicalFile(path, MimeTypeMap.GetMimeType(format), Path.GetFileName(path), true);
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "pageNum", "apiKey"])]
    [AllowAnonymous]
    public async Task<ActionResult> GetThumbnail(int chapterId, int pageNum, string apiKey)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return BadRequest();
        var chapter = await _cacheService.Ensure(chapterId, true);
        if (chapter == null) return NoContent();
        var images = _cacheService.GetCachedPages(chapterId);

        var path = await _readerService.GetThumbnail(chapter, pageNum, images);
        var format = Path.GetExtension(path);
        return PhysicalFile(path, MimeTypeMap.GetMimeType(format), Path.GetFileName(path), true);
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["seriesId", "page", "apiKey"])]
    [AllowAnonymous]
    public async Task<ActionResult> GetBookmarkImage(int seriesId, string apiKey, int page)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId == 0) return Unauthorized();

        if (page < 0) page = 0;
        var totalPages = await _cacheService.CacheBookmarkForSeries(userId, seriesId);
        if (page > totalPages)
        {
            page = totalPages;
        }

        try
        {
            var path = _cacheService.GetCachedBookmarkPagePath(seriesId, page);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest(await _localizationService.Translate(userId, "no-image-for-page", page));
            var format = Path.GetExtension(path);

            return PhysicalFile(path, MimeTypeMap.GetMimeType(format), Path.GetFileName(path));
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
    /// <param name="includeDimensions">Include file dimensions. Only useful for image based reading</param>
    /// <returns></returns>
    [HttpGet("chapter-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId", "extractPdf", "includeDimensions"
    ])]
    public async Task<ActionResult<ChapterInfoDto>> GetChapterInfo(int chapterId, bool extractPdf = false, bool includeDimensions = false)
    {
        if (chapterId <= 0) return Ok(null); // This can happen occasionally from UI, we should just ignore
        var chapter = await _cacheService.Ensure(chapterId, extractPdf);
        if (chapter == null) return NoContent();

        var dto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "perform-scan"));
        var mangaFile = chapter.Files.First();

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(dto.SeriesId, User.GetUserId());
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
        } else if (!info.IsSpecial && info.VolumeNumber.Equals(Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume))
        {
            info.Subtitle = ReaderService.FormatChapterName(info.LibraryType, true, true) + info.ChapterNumber;
        }
        else
        {
            info.Subtitle = await _localizationService.Translate(User.GetUserId(), "volume-num", info.VolumeNumber);
            if (!info.ChapterNumber.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultChapter))
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
        var totalPages = await _cacheService.CacheBookmarkForSeries(User.GetUserId(), seriesId);
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        try
        {
            await _readerService.MarkSeriesAsRead(user, markReadDto.SeriesId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        await _readerService.MarkSeriesAsUnread(user, markReadDto.SeriesId);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        await _readerService.MarkChaptersAsUnread(user, markVolumeReadDto.SeriesId, chapters);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
        if (user == null) return Unauthorized();
        try
        {
            await _readerService.MarkChaptersAsRead(user, markVolumeReadDto.SeriesId, chapters);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
        await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
            MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, markVolumeReadDto.SeriesId,
                markVolumeReadDto.VolumeId, 0, chapters.Sum(c => c.Pages)));

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }
        var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
        await _readerService.MarkChaptersAsRead(user, dto.SeriesId, chapters.ToList());

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));
    }

    /// <summary>
    /// Marks all chapters within a list of series as Read.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("mark-multiple-series-read")]
    public async Task<ActionResult> MarkMultipleSeriesAsRead(MarkMultipleSeriesAsReadDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
        foreach (var volume in volumes)
        {
            await _readerService.MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
        if (user == null) return Unauthorized();
        user.Progresses ??= new List<AppUserProgress>();

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));
    }

    /// <summary>
    /// Returns Progress (page number) for a chapter for the logged in user
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("get-progress")]
    public async Task<ActionResult<ProgressDto>> GetProgress(int chapterId)
    {
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, User.GetUserId());
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
        var userId = User.GetUserId();
        if (!await _readerService.SaveReadingProgress(progressDto, userId))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-read-progress"));


        return Ok(true);
    }

    /// <summary>
    /// Continue point is the chapter which you should start reading again from. If there is no progress on a series, then the first chapter will be returned (non-special unless only specials).
    /// Otherwise, loop through the chapters and volumes in order to find the next chapter which has progress.
    /// </summary>
    /// <returns></returns>
    [HttpGet("continue-point")]
    public async Task<ActionResult<ChapterDto>> GetContinuePoint(int seriesId)
    {
        return Ok(await _readerService.GetContinuePoint(seriesId, User.GetUserId()));
    }

    /// <summary>
    /// Returns if the user has reading progress on the Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("has-progress")]
    public async Task<ActionResult<bool>> HasProgress(int seriesId)
    {
        return Ok(await _unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(seriesId, User.GetUserId()));
    }

    /// <summary>
    /// Returns a list of bookmarked pages for a given Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarks(int chapterId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForChapter(User.GetUserId(), chapterId));
    }

    /// <summary>
    /// Returns a list of all bookmarked pages for a User
    /// </summary>
    /// <param name="filterDto">Only supports SeriesNameQuery</param>
    /// <returns></returns>
    [HttpPost("all-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetAllBookmarks(FilterV2Dto filterDto)
    {
        return Ok(await _unitOfWork.UserRepository.GetAllBookmarkDtos(User.GetUserId(), filterDto));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("remove-bookmarks")]
    public async Task<ActionResult> RemoveBookmarks(RemoveBookmarkForSeriesDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null) return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Removes all bookmarks for all chapters linked to a Series
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("bulk-remove-bookmarks")]
    public async Task<ActionResult> BulkRemoveBookmarks(BulkRemoveBookmarkForSeriesDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
        if (user == null) return Unauthorized();
        if (user.Bookmarks == null) return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-clear-bookmarks"));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet("volume-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForVolume(int volumeId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForVolume(User.GetUserId(), volumeId));
    }

    /// <summary>
    /// Returns all bookmarked pages for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("series-bookmarks")]
    public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForSeries(int seriesId)
    {
        return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForSeries(User.GetUserId(), seriesId));
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
        // Don't let user save past total pages.
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
        if (user == null) return new UnauthorizedResult();

        if (!await _accountService.HasBookmarkPermission(user))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "bookmark-permission"));

        var chapter = await _cacheService.Ensure(bookmarkDto.ChapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "cache-file-find"));

        bookmarkDto.Page = _readerService.CapPageToChapter(chapter, bookmarkDto.Page);
        var path = _cacheService.GetCachedPagePath(chapter.Id, bookmarkDto.Page);

        if (!await _bookmarkService.BookmarkPage(user, bookmarkDto, path))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "bookmark-save"));

        BackgroundJob.Enqueue(() => _cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));
        return Ok();
    }

    /// <summary>
    /// Removes a bookmarked page for a Chapter
    /// </summary>
    /// <param name="bookmarkDto"></param>
    /// <returns></returns>
    [HttpPost("unbookmark")]
    public async Task<ActionResult> UnBookmarkPage(BookmarkDto bookmarkDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
        if (user == null) return new UnauthorizedResult();
        if (user.Bookmarks == null || user.Bookmarks.Count == 0) return Ok();

        if (!await _accountService.HasBookmarkPermission(user))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "bookmark-permission"));

        if (!await _bookmarkService.RemoveBookmarkPage(user, bookmarkDto))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "bookmark-save"));
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
        return await _readerService.GetNextChapterIdAsync(seriesId, volumeId, currentChapterId, User.GetUserId());
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
        return await _readerService.GetPrevChapterIdAsync(seriesId, volumeId, currentChapterId, User.GetUserId());
    }

    /// <summary>
    /// For the current user, returns an estimate on how long it would take to finish reading the series.
    /// </summary>
    /// <remarks>For Epubs, this does not check words inside a chapter due to overhead so may not work in all cases.</remarks>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("time-left")]
    [ResponseCache(CacheProfileName = "Hour", VaryByQueryKeys = ["seriesId"])]
    public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletion(int seriesId)
    {
        var userId = User.GetUserId();
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        // Get all sum of all chapters with progress that is complete then subtract from series. Multiply by modifiers
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(seriesId, userId);
        if (series.Format == MangaFormat.Epub)
        {
            var chapters =
                await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(progress.Select(p => p.ChapterId).ToList());
            // Word count
            var progressCount = chapters.Sum(c => c.WordCount);
            var wordsLeft = series.WordCount - progressCount;
            return _readerService.GetTimeEstimate(wordsLeft, 0, true);
        }

        var progressPageCount = progress.Sum(p => p.PagesRead);
        var pagesLeft = series.Pages - progressPageCount;
        return _readerService.GetTimeEstimate(0, pagesLeft, false);
    }

    /// <summary>
    /// Returns the user's personal table of contents for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("ptoc")]
    public ActionResult<IEnumerable<PersonalToCDto>> GetPersonalToC(int chapterId)
    {
        return Ok(_unitOfWork.UserTableOfContentRepository.GetPersonalToC(User.GetUserId(), chapterId));
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
        var userId = User.GetUserId();
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
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(await _localizationService.Translate(userId, "name-required"));
        if (dto.PageNumber < 0) return BadRequest(await _localizationService.Translate(userId, "valid-number"));
        if (await _unitOfWork.UserTableOfContentRepository.IsUnique(userId, dto.ChapterId, dto.PageNumber,
                dto.Title.Trim()))
        {
            return BadRequest(await _localizationService.Translate(userId, "duplicate-bookmark"));
        }

        _unitOfWork.UserTableOfContentRepository.Attach(new AppUserTableOfContent()
        {
            Title = dto.Title.Trim(),
            ChapterId = dto.ChapterId,
            PageNumber = dto.PageNumber,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            BookScrollId = dto.BookScrollId,
            AppUserId = userId
        });
        await _unitOfWork.CommitAsync();
        return Ok();
    }

    /// <summary>
    /// Get all progress events for a given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("all-chapter-progress")]
    public async Task<ActionResult<IEnumerable<FullProgressDto>>> GetProgressForChapter(int chapterId)
    {
        var userId = User.IsInRole(PolicyConstants.AdminRole) ? 0 : User.GetUserId();
        return Ok(await _unitOfWork.AppUserProgressRepository.GetUserProgressForChapter(chapterId, userId));

    }
}
