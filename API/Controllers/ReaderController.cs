using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
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

        /// <inheritdoc />
        public ReaderController(ICacheService cacheService,
            IUnitOfWork unitOfWork, ILogger<ReaderController> logger,
            IReaderService readerService, IBookmarkService bookmarkService)
        {
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _readerService = readerService;
            _bookmarkService = bookmarkService;
        }

        /// <summary>
        /// Returns the PDF for the chapterId.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("pdf")]
        [ResponseCache(CacheProfileName = "Hour")]
        public async Task<ActionResult> GetPdf(int chapterId)
        {
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding pdf file for reading");

            // Validate the user has access to the PDF
            var series = await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapter.Id,
                await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername()));
            if (series == null) return BadRequest("Invalid Access");

            try
            {

                var path = _cacheService.GetCachedFile(chapter);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"Pdf doesn't exist when it should.");

                return PhysicalFile(path, "application/pdf", Path.GetFileName(path), true);
            }
            catch (Exception)
            {
                _cacheService.CleanupChapters(new []{ chapterId });
                throw;
            }
        }

        /// <summary>
        /// Returns an image for a given chapter. Side effect: This will cache the chapter images for reading.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet("image")]
        [ResponseCache(CacheProfileName = "Hour")]
        [AllowAnonymous]
        public async Task<ActionResult> GetImage(int chapterId, int page)
        {
            if (page < 0) page = 0;
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding image file for reading");

            try
            {
                var path = _cacheService.GetCachedPagePath(chapter, page);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {page}. Try refreshing to allow re-cache.");
                var format = Path.GetExtension(path).Replace(".", "");

                return PhysicalFile(path, "image/" + format, Path.GetFileName(path), true);
            }
            catch (Exception)
            {
                _cacheService.CleanupChapters(new []{ chapterId });
                throw;
            }
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
        [ResponseCache(CacheProfileName = "Hour")]
        [AllowAnonymous]
        public async Task<ActionResult> GetBookmarkImage(int seriesId, string apiKey, int page)
        {
            if (page < 0) page = 0;
            var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);

            // NOTE: I'm not sure why I need this flow here
            var totalPages = await _cacheService.CacheBookmarkForSeries(userId, seriesId);
            if (page > totalPages)
            {
                page = totalPages;
            }

            try
            {
                var path = _cacheService.GetCachedBookmarkPagePath(seriesId, page);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {page}");
                var format = Path.GetExtension(path).Replace(".", "");

                return PhysicalFile(path, "image/" + format, Path.GetFileName(path));
            }
            catch (Exception)
            {
                _cacheService.CleanupBookmarks(new []{ seriesId });
                throw;
            }
        }

        /// <summary>
        /// Returns various information about a Chapter. Side effect: This will cache the chapter images for reading.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("chapter-info")]
        public async Task<ActionResult<ChapterInfoDto>> GetChapterInfo(int chapterId)
        {
            if (chapterId <= 0) return null; // This can happen occasionally from UI, we should just ignore
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("Could not find Chapter");

            var dto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
            if (dto == null) return BadRequest("Please perform a scan on this series or library and try again");
            var mangaFile = (await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId)).First();

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
                ChapterTitle = dto.ChapterTitle ?? string.Empty,
                Subtitle = string.Empty,
                Title = dto.SeriesName
            };

            if (info.ChapterTitle is {Length: > 0}) {
                info.Title += " - " + info.ChapterTitle;
            }

            if (info.IsSpecial && dto.VolumeNumber.Equals(Parser.Parser.DefaultVolume))
            {
                info.Subtitle = info.FileName;
            } else if (!info.IsSpecial && info.VolumeNumber.Equals(Parser.Parser.DefaultVolume))
            {
                info.Subtitle = _readerService.FormatChapterName(info.LibraryType, true, true) + info.ChapterNumber;
            }
            else
            {
                info.Subtitle = "Volume " + info.VolumeNumber;
                if (!info.ChapterNumber.Equals(Parser.Parser.DefaultChapter))
                {
                    info.Subtitle += " " + _readerService.FormatChapterName(info.LibraryType, true, true) +
                                     info.ChapterNumber;
                }
            }

            return Ok(info);
        }

        /// <summary>
        /// Returns various information about all bookmark files for a Series. Side effect: This will cache the bookmark images for reading.
        /// </summary>
        /// <param name="seriesId">Series Id for all bookmarks</param>
        /// <returns></returns>
        [HttpGet("bookmark-info")]
        public async Task<ActionResult<BookmarkInfoDto>> GetBookmarkInfo(int seriesId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var totalPages = await _cacheService.CacheBookmarkForSeries(user.Id, seriesId);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.None);

            return Ok(new BookmarkInfoDto()
            {
                SeriesName = series.Name,
                SeriesFormat = series.Format,
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                Pages = totalPages,
            });
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
            await _readerService.MarkSeriesAsRead(user, markReadDto.SeriesId);

            if (!await _unitOfWork.CommitAsync()) return BadRequest("There was an issue saving progress");

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
            await _readerService.MarkSeriesAsUnread(user, markReadDto.SeriesId);

            if (!await _unitOfWork.CommitAsync()) return BadRequest("There was an issue saving progress");

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

            var chapters = await _unitOfWork.ChapterRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
            await _readerService.MarkChaptersAsUnread(user, markVolumeReadDto.SeriesId, chapters);

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
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
            await _readerService.MarkChaptersAsRead(user, markVolumeReadDto.SeriesId, chapters);

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
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
            user.Progresses ??= new List<AppUserProgress>();

            var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
            foreach (var chapterId in dto.ChapterIds)
            {
                chapterIds.Add(chapterId);
            }
            var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
            await _readerService.MarkChaptersAsRead(user, dto.SeriesId, chapters);

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
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
            user.Progresses ??= new List<AppUserProgress>();

            var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
            foreach (var chapterId in dto.ChapterIds)
            {
                chapterIds.Add(chapterId);
            }
            var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds);
            await _readerService.MarkChaptersAsUnread(user, dto.SeriesId, chapters);

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
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
            user.Progresses ??= new List<AppUserProgress>();

            var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
            foreach (var volume in volumes)
            {
                await _readerService.MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
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
            user.Progresses ??= new List<AppUserProgress>();

            var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(dto.SeriesIds.ToArray(), true);
            foreach (var volume in volumes)
            {
                await _readerService.MarkChaptersAsUnread(user, volume.SeriesId, volume.Chapters);
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
        }

        /// <summary>
        /// Returns Progress (page number) for a chapter for the logged in user
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("get-progress")]
        public async Task<ActionResult<ProgressDto>> GetProgress(int chapterId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
            var progressBookmark = new ProgressDto()
            {
                PageNum = 0,
                ChapterId = chapterId,
                VolumeId = 0,
                SeriesId = 0
            };
            if (user.Progresses == null) return Ok(progressBookmark);
            var progress = user.Progresses.FirstOrDefault(x => x.AppUserId == user.Id && x.ChapterId == chapterId);

            if (progress != null)
            {
                progressBookmark.SeriesId = progress.SeriesId;
                progressBookmark.VolumeId = progress.VolumeId;
                progressBookmark.PageNum = progress.PagesRead;
                progressBookmark.BookScrollId = progress.BookScrollId;
            }
            return Ok(progressBookmark);
        }

        /// <summary>
        /// Save page against Chapter for logged in user
        /// </summary>
        /// <param name="progressDto"></param>
        /// <returns></returns>
        [HttpPost("progress")]
        public async Task<ActionResult> BookmarkProgress(ProgressDto progressDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            if (await _readerService.SaveReadingProgress(progressDto, user.Id)) return Ok(true);

            return BadRequest("Could not save progress");
        }

        /// <summary>
        /// Continue point is the chapter which you should start reading again from. If there is no progress on a series, then the first chapter will be returned (non-special unless only specials).
        /// Otherwise, loop through the chapters and volumes in order to find the next chapter which has progress.
        /// </summary>
        /// <returns></returns>
        [HttpGet("continue-point")]
        public async Task<ActionResult<ChapterDto>> GetContinuePoint(int seriesId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());

            return Ok(await _readerService.GetContinuePoint(seriesId, userId));
        }

        /// <summary>
        /// Returns if the user has reading progress on the Series
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        [HttpGet("has-progress")]
        public async Task<ActionResult<ChapterDto>> HasProgress(int seriesId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(seriesId, userId));
        }

        /// <summary>
        /// Marks every chapter that is sorted below the passed number as Read. This will not mark any specials as read.
        /// </summary>
        /// <remarks>This is built for Tachiyomi and is not expected to be called by any other place</remarks>
        /// <returns></returns>
        [Obsolete("Deprecated. Use 'Tachiyomi/mark-chapter-until-as-read'")]
        [HttpPost("mark-chapter-until-as-read")]
        public async Task<ActionResult<bool>> MarkChaptersUntilAsRead(int seriesId, float chapterNumber)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Progress);
            user.Progresses ??= new List<AppUserProgress>();

            // Tachiyomi sends chapter 0.0f when there's no chapters read.
            // Due to the encoding for volumes this marks all chapters in volume 0 (loose chapters) as read so we ignore it
            if (chapterNumber == 0.0f) return true;

            if (chapterNumber < 1.0f)
            {
                // This is a hack to track volume number. We need to map it back by x100
                var volumeNumber = int.Parse($"{chapterNumber * 100f}");
                await _readerService.MarkVolumesUntilAsRead(user, seriesId, volumeNumber);
            }
            else
            {
                await _readerService.MarkChaptersUntilAsRead(user, seriesId, chapterNumber);
            }


            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges()) return Ok(true);
            if (await _unitOfWork.CommitAsync()) return Ok(true);

            await _unitOfWork.RollbackAsync();
            return Ok(false);
        }


        /// <summary>
        /// Returns a list of bookmarked pages for a given Chapter
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("get-bookmarks")]
        public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarks(int chapterId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
            if (user.Bookmarks == null) return Ok(Array.Empty<BookmarkDto>());
            return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForChapter(user.Id, chapterId));
        }

        /// <summary>
        /// Returns a list of all bookmarked pages for a User
        /// </summary>
        /// <returns></returns>
        [HttpGet("get-all-bookmarks")]
        public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetAllBookmarks()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
            if (user.Bookmarks == null) return Ok(Array.Empty<BookmarkDto>());
            return Ok(await _unitOfWork.UserRepository.GetAllBookmarkDtos(user.Id));
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
            if (user.Bookmarks == null) return Ok("Nothing to remove");

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

            return BadRequest("Could not clear bookmarks");
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
            if (user.Bookmarks == null) return Ok("Nothing to remove");

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

            return BadRequest("Could not clear bookmarks");
        }

        /// <summary>
        /// Returns all bookmarked pages for a given volume
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [HttpGet("get-volume-bookmarks")]
        public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForVolume(int volumeId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
            if (user.Bookmarks == null) return Ok(Array.Empty<BookmarkDto>());
            return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForVolume(user.Id, volumeId));
        }

        /// <summary>
        /// Returns all bookmarked pages for a given series
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        [HttpGet("get-series-bookmarks")]
        public async Task<ActionResult<IEnumerable<BookmarkDto>>> GetBookmarksForSeries(int seriesId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
            if (user.Bookmarks == null) return Ok(Array.Empty<BookmarkDto>());

            return Ok(await _unitOfWork.UserRepository.GetBookmarkDtosForSeries(user.Id, seriesId));
        }

        /// <summary>
        /// Bookmarks a page against a Chapter
        /// </summary>
        /// <param name="bookmarkDto"></param>
        /// <returns></returns>
        [HttpPost("bookmark")]
        public async Task<ActionResult> BookmarkPage(BookmarkDto bookmarkDto)
        {
            // Don't let user save past total pages.
            bookmarkDto.Page = await _readerService.CapPageToChapter(bookmarkDto.ChapterId, bookmarkDto.Page);
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Bookmarks);
            var chapter = await _cacheService.Ensure(bookmarkDto.ChapterId);
            if (chapter == null) return BadRequest("Could not find cached image. Reload and try again.");
            var path = _cacheService.GetCachedPagePath(chapter, bookmarkDto.Page);

            if (await _bookmarkService.BookmarkPage(user, bookmarkDto, path))
            {
                BackgroundJob.Enqueue(() => _cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));
                return Ok();
            }

            return BadRequest("Could not save bookmark");
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
            if (user.Bookmarks == null) return Ok();

            if (await _bookmarkService.RemoveBookmarkPage(user, bookmarkDto))
            {
                BackgroundJob.Enqueue(() => _cacheService.CleanupBookmarkCache(bookmarkDto.SeriesId));
                return Ok();
            }

            return BadRequest("Could not remove bookmark");
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
        [HttpGet("next-chapter")]
        public async Task<ActionResult<int>> GetNextChapter(int seriesId, int volumeId, int currentChapterId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return await _readerService.GetNextChapterIdAsync(seriesId, volumeId, currentChapterId, userId);
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
        [HttpGet("prev-chapter")]
        public async Task<ActionResult<int>> GetPreviousChapter(int seriesId, int volumeId, int currentChapterId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return await _readerService.GetPrevChapterIdAsync(seriesId, volumeId, currentChapterId, userId);
        }

        /// <summary>
        /// For the current user, returns an estimate on how long it would take to finish reading the series.
        /// </summary>
        /// <remarks>For Epubs, this does not check words inside a chapter due to overhead so may not work in all cases.</remarks>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        [HttpGet("time-left")]
        public async Task<ActionResult<HourEstimateRangeDto>> GetEstimateToCompletion(int seriesId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
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

    }
}
