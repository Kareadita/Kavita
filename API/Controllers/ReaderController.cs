using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs;
using API.DTOs.Reader;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();
        private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();
        private readonly NaturalSortComparer _naturalSortComparer = new NaturalSortComparer();

        public ReaderController(IDirectoryService directoryService, ICacheService cacheService, IUnitOfWork unitOfWork)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("image")]
        public async Task<ActionResult> GetImage(int chapterId, int page)
        {
            if (page < 0) return BadRequest("Page cannot be less than 0");
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding image file for reading");

            var (path, _) = await _cacheService.GetCachedPagePath(chapter, page);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {page}");

            var content = await _directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path).Replace(".", "");

            // Calculates SHA1 Hash for byte[]
            Response.AddCacheHeader(content);

            return File(content, "image/" + format);
        }

        [HttpGet("chapter-info")]
        public async Task<ActionResult<ChapterInfoDto>> GetChapterInfo(int chapterId)
        {
            // PERF: Write this in one DB call
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("Could not find Chapter");
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(chapter.VolumeId);
            if (volume == null) return BadRequest("Could not find Volume");
            var (_, mangaFile) = await _cacheService.GetCachedPagePath(chapter, 0);
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);

            return Ok(new ChapterInfoDto()
            {
                ChapterNumber =  chapter.Range,
                VolumeNumber = volume.Number + string.Empty,
                VolumeId = volume.Id,
                FileName = Path.GetFileName(mangaFile.FilePath),
                SeriesName = series?.Name,
                IsSpecial = chapter.IsSpecial,
                Pages = chapter.Pages,
            });
        }

        [HttpGet("get-bookmark")]
        public async Task<ActionResult<BookmarkDto>> GetBookmark(int chapterId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var bookmark = new BookmarkDto()
            {
                PageNum = 0,
                ChapterId = chapterId,
                VolumeId = 0,
                SeriesId = 0
            };
            if (user.Progresses == null) return Ok(bookmark);
            var progress = user.Progresses.SingleOrDefault(x => x.AppUserId == user.Id && x.ChapterId == chapterId);

            if (progress != null)
            {
                bookmark.SeriesId = progress.SeriesId;
                bookmark.VolumeId = progress.VolumeId;
                bookmark.PageNum = progress.PagesRead;
                bookmark.BookScrollId = progress.BookScrollId;
            }
            return Ok(bookmark);
        }

        [HttpPost("mark-read")]
        public async Task<ActionResult> MarkRead(MarkReadDto markReadDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var volumes = await _unitOfWork.SeriesRepository.GetVolumes(markReadDto.SeriesId);
            user.Progresses ??= new List<AppUserProgress>();
            foreach (var volume in volumes)
            {
                foreach (var chapter in volume.Chapters)
                {
                    var userProgress = GetUserProgressForChapter(user, chapter);

                    if (userProgress == null)
                    {
                        user.Progresses.Add(new AppUserProgress
                        {
                            PagesRead = chapter.Pages,
                            VolumeId = volume.Id,
                            SeriesId = markReadDto.SeriesId,
                            ChapterId = chapter.Id
                        });
                    }
                    else
                    {
                        userProgress.PagesRead = chapter.Pages;
                        userProgress.SeriesId = markReadDto.SeriesId;
                        userProgress.VolumeId = volume.Id;
                    }
                }
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }


            return BadRequest("There was an issue saving progress");
        }

        private static AppUserProgress GetUserProgressForChapter(AppUser user, Chapter chapter)
        {
            AppUserProgress userProgress = null;
            try
            {
                userProgress =
                    user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);
            }
            catch (Exception)
            {
                // There is a very rare chance that user progress will duplicate current row. If that happens delete one with less pages
                var progresses = user.Progresses.Where(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id).ToList();
                if (progresses.Count > 1)
                {
                    user.Progresses = new List<AppUserProgress>()
                    {
                        user.Progresses.First()
                    };
                    userProgress = user.Progresses.First();
                }
            }

            return userProgress;
        }

        [HttpPost("mark-unread")]
        public async Task<ActionResult> MarkUnread(MarkReadDto markReadDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var volumes = await _unitOfWork.SeriesRepository.GetVolumes(markReadDto.SeriesId);
            user.Progresses ??= new List<AppUserProgress>();
            foreach (var volume in volumes)
            {
                foreach (var chapter in volume.Chapters)
                {
                    var userProgress = GetUserProgressForChapter(user, chapter);

                    if (userProgress == null) continue;
                    userProgress.PagesRead = 0;
                    userProgress.SeriesId = markReadDto.SeriesId;
                    userProgress.VolumeId = volume.Id;
                }
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }


            return BadRequest("There was an issue saving progress");
        }

        [HttpPost("mark-volume-read")]
        public async Task<ActionResult> MarkVolumeAsRead(MarkVolumeReadDto markVolumeReadDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            var chapters = await _unitOfWork.VolumeRepository.GetChaptersAsync(markVolumeReadDto.VolumeId);
            foreach (var chapter in chapters)
            {
                user.Progresses ??= new List<AppUserProgress>();
                var userProgress = user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);

                if (userProgress == null)
                {
                    user.Progresses.Add(new AppUserProgress
                    {
                        PagesRead = chapter.Pages,
                        VolumeId = markVolumeReadDto.VolumeId,
                        SeriesId = markVolumeReadDto.SeriesId,
                        ChapterId = chapter.Id
                    });
                }
                else
                {
                    userProgress.PagesRead = chapter.Pages;
                    userProgress.SeriesId = markVolumeReadDto.SeriesId;
                    userProgress.VolumeId = markVolumeReadDto.VolumeId;
                }
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
        }

        [HttpPost("bookmark")]
        public async Task<ActionResult> Bookmark(BookmarkDto bookmarkDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            // Don't let user bookmark past total pages.
            var chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(bookmarkDto.ChapterId);
            if (bookmarkDto.PageNum > chapter.Pages)
            {
                bookmarkDto.PageNum = chapter.Pages;
            }

            if (bookmarkDto.PageNum < 0)
            {
                bookmarkDto.PageNum = 0;
            }


            try
            {
                // TODO: Look into creating a progress entry when a new item is added to the DB so we can just look it up and modify it
               user.Progresses ??= new List<AppUserProgress>();
               var userProgress =
                  user.Progresses.SingleOrDefault(x => x.ChapterId == bookmarkDto.ChapterId && x.AppUserId == user.Id);

               if (userProgress == null)
               {
                  user.Progresses.Add(new AppUserProgress
                  {
                     PagesRead = bookmarkDto.PageNum,
                     VolumeId = bookmarkDto.VolumeId,
                     SeriesId = bookmarkDto.SeriesId,
                     ChapterId = bookmarkDto.ChapterId,
                     BookScrollId = bookmarkDto.BookScrollId,
                     LastModified = DateTime.Now
                  });
               }
               else
               {
                  userProgress.PagesRead = bookmarkDto.PageNum;
                  userProgress.SeriesId = bookmarkDto.SeriesId;
                  userProgress.VolumeId = bookmarkDto.VolumeId;
                  userProgress.BookScrollId = bookmarkDto.BookScrollId;
                  userProgress.LastModified = DateTime.Now;
               }

               _unitOfWork.UserRepository.Update(user);

               if (await _unitOfWork.CommitAsync())
               {
                  return Ok();
               }
            }
            catch (Exception)
            {
               await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Could not save progress");
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
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var volumes = await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id);
            var currentVolume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var currentChapter = await _unitOfWork.VolumeRepository.GetChapterAsync(currentChapterId);
            if (currentVolume.Number == 0)
            {
                // Handle specials by sorting on their Filename aka Range
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.Range, _naturalSortComparer), currentChapter.Number);
                if (chapterId > 0) return Ok(chapterId);
            }

            foreach (var volume in volumes)
            {
                if (volume.Number == currentVolume.Number && volume.Chapters.Count > 1)
                {
                    // Handle Chapters within current Volume
                    // In this case, i need 0 first because 0 represents a full volume file.
                    var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting), currentChapter.Number);
                    if (chapterId > 0) return Ok(chapterId);
                }

                if (volume.Number == currentVolume.Number + 1)
                {
                    // Handle Chapters within next Volume
                    // ! When selecting the chapter for the next volume, we need to make sure a c0 comes before a c1+
                    var chapters = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).ToList();
                    if (currentChapter.Number.Equals("0") && chapters.Last().Number.Equals("0"))
                    {
                        return chapters.Last().Id;
                    }

                    return Ok(chapters.FirstOrDefault()?.Id);
                }
            }
            return Ok(-1);
        }

        private static int GetNextChapterId(IEnumerable<Chapter> chapters, string currentChapterNumber)
        {
            var next = false;
            var chaptersList = chapters.ToList();
            foreach (var chapter in chaptersList)
            {
                if (next)
                {
                    return chapter.Id;
                }
                if (currentChapterNumber.Equals(chapter.Number)) next = true;
            }

            return -1;
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
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var volumes = await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id);
            var currentVolume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var currentChapter = await _unitOfWork.VolumeRepository.GetChapterAsync(currentChapterId);

            if (currentVolume.Number == 0)
            {
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.Range, _naturalSortComparer).Reverse(), currentChapter.Number);
                if (chapterId > 0) return Ok(chapterId);
            }

            foreach (var volume in volumes.Reverse())
            {
                if (volume.Number == currentVolume.Number)
                {
                    var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).Reverse(), currentChapter.Number);
                    if (chapterId > 0) return Ok(chapterId);
                }
                if (volume.Number == currentVolume.Number - 1)
                {
                    return Ok(volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).LastOrDefault()?.Id);
                }
            }
            return Ok(-1);
        }

    }
}
