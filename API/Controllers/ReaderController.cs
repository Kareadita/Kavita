﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ReaderController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public ReaderController(IDirectoryService directoryService, ICacheService cacheService,
            ILogger<ReaderController> logger, IUnitOfWork unitOfWork)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("image")]
        public async Task<ActionResult> GetImage(int chapterId, int page)
        {
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding image file for reading");

            var (path, mangaFile) = await _cacheService.GetCachedPagePath(chapter, page);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {page}");

            var content = await _directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path).Replace(".", "");

            // Look into HttpContext.Cache so we can utilize a memorystream for Zip entries (want to limit response time by 300ms)
            // Calculates SHA1 Hash for byte[]
            using var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            Response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
            Response.Headers.Add("Cache-Control", "private");

            return File(content, "image/" + format);
        }

        [HttpGet("chapter-path")]
        public async Task<ActionResult<string>> GetImagePath(int chapterId)
        {
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding image file for reading");

            var (path, mangaFile) = await _cacheService.GetCachedPagePath(chapter, 0);
            return Ok(mangaFile.FilePath);
        }

        [HttpGet("get-bookmark")]
        public async Task<ActionResult<int>> GetBookmark(int chapterId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            if (user.Progresses == null) return Ok(0);
            var progress = user.Progresses.SingleOrDefault(x => x.AppUserId == user.Id && x.ChapterId == chapterId);

            return Ok(progress?.PagesRead ?? 0);
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
                    var userProgress = user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);
                    if (userProgress == null) // I need to get all chapters and generate new user progresses for them? 
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

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }
            
            
            return BadRequest("There was an issue saving progress");
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
                    var userProgress = user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);
                    if (userProgress == null)
                    {
                        user.Progresses.Add(new AppUserProgress
                        {
                            PagesRead = 0,
                            VolumeId = volume.Id,
                            SeriesId = markReadDto.SeriesId,
                            ChapterId = chapter.Id
                        });
                    }
                    else
                    {
                        userProgress.PagesRead = 0;
                        userProgress.SeriesId = markReadDto.SeriesId;
                        userProgress.VolumeId = volume.Id;
                    }
                }
            }
            
            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }
            
            
            return BadRequest("There was an issue saving progress");
        }

        [HttpPost("mark-volume-read")]
        public async Task<ActionResult> MarkVolumeAsRead(MarkVolumeReadDto markVolumeReadDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogDebug("Saving {UserName} progress for Volume {VolumeID} to read", user.UserName, markVolumeReadDto.VolumeId);
            
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

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
        }    

        [HttpPost("bookmark")]
        public async Task<ActionResult> Bookmark(BookmarkDto bookmarkDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogDebug("Saving {UserName} progress for Chapter {ChapterId} to page {PageNum}", user.UserName, bookmarkDto.ChapterId, bookmarkDto.PageNum);
            
            // Don't let user bookmark past total pages.
            var chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(bookmarkDto.ChapterId);
            if (bookmarkDto.PageNum > chapter.Pages)
            {
                return BadRequest("Can't bookmark past max pages");
            }

            if (bookmarkDto.PageNum < 0)
            {
                return BadRequest("Can't bookmark less than 0");
            }
            
            
            user.Progresses ??= new List<AppUserProgress>();
            var userProgress = user.Progresses.SingleOrDefault(x => x.ChapterId == bookmarkDto.ChapterId && x.AppUserId == user.Id);

            if (userProgress == null)
            {
                user.Progresses.Add(new AppUserProgress
                {
                    PagesRead = bookmarkDto.PageNum,
                    VolumeId = bookmarkDto.VolumeId,
                    SeriesId = bookmarkDto.SeriesId,
                    ChapterId = bookmarkDto.ChapterId
                });
            }
            else
            {
                userProgress.PagesRead = bookmarkDto.PageNum;
                userProgress.SeriesId = bookmarkDto.SeriesId;
                userProgress.VolumeId = bookmarkDto.VolumeId;
            }

            _unitOfWork.UserRepository.Update(user);

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }

            return BadRequest("Could not save progress");
        }
    }
}