using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
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
        private readonly IMapper _mapper;

        public ReaderController(IDirectoryService directoryService, ICacheService cacheService,
            ILogger<ReaderController> logger, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int chapterId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            var chapter = await _cacheService.Ensure(chapterId);

            if (chapter == null) return BadRequest("There was an issue finding image file for reading.");

            var (path, mangaFile) = await _cacheService.GetCachedPagePath(chapter, page);
            if (string.IsNullOrEmpty(path)) return BadRequest($"No such image for page {page}");
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;
            //file.Chapter = chapter.Number;
            file.MangaFileName = mangaFile.FilePath;

            return Ok(file);
        }

        [HttpGet("get-bookmark")]
        public async Task<ActionResult<int>> GetBookmark(int chapterId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            if (user.Progresses == null) return Ok(0);
            var progress = user.Progresses.SingleOrDefault(x => x.AppUserId == user.Id && x.ChapterId == chapterId);

            if (progress != null) return Ok(progress.PagesRead);
            
            return Ok(0);
        }

        [HttpPost("bookmark")]
        public async Task<ActionResult> Bookmark(BookmarkDto bookmarkDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogInformation($"Saving {user.UserName} progress for Chapter {bookmarkDto.ChapterId} to page {bookmarkDto.PageNum}");
            
            // TODO: Don't let user bookmark past total pages.

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