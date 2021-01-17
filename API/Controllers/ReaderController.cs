using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ReaderController> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly DataContext _dataContext; // TODO: Refactor code into repo
        private readonly IUserRepository _userRepository;
        private readonly ISeriesRepository _seriesRepository;

        public ReaderController(IDirectoryService directoryService, ICacheService cacheService,
            ILogger<ReaderController> logger, UserManager<AppUser> userManager, DataContext dataContext,
            IUserRepository userRepository, ISeriesRepository seriesRepository)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
            _logger = logger;
            _userManager = userManager;
            _dataContext = dataContext;
            _userRepository = userRepository;
            _seriesRepository = seriesRepository;
        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int volumeId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            var volume = await _cacheService.Ensure(volumeId);

            var path = _cacheService.GetCachedPagePath(volume, page);
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;

            return Ok(file);
        }

        [HttpGet("get-bookmark")]
        public async Task<ActionResult<int>> GetBookmark(int volumeId)
        {
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
            if (user.Progresses == null) return Ok(0);
            var progress = user.Progresses.SingleOrDefault(x => x.AppUserId == user.Id && x.VolumeId == volumeId);

            if (progress != null) return Ok(progress.PagesRead);
            
            return Ok(0);
        }

        [HttpPost("bookmark")]
        public async Task<ActionResult> Bookmark(BookmarkDto bookmarkDto)
        {
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogInformation($"Saving {user.UserName} progress for {bookmarkDto.VolumeId} to page {bookmarkDto.PageNum}");

            user.Progresses ??= new List<AppUserProgress>();
            var userProgress = user.Progresses.SingleOrDefault(x => x.VolumeId == bookmarkDto.VolumeId && x.AppUserId == user.Id);

            if (userProgress == null)
            {
                
                user.Progresses.Add(new AppUserProgress
                {
                    PagesRead = bookmarkDto.PageNum, // TODO: PagesRead is misleading. Should it be PageNumber or PagesRead (+1)?
                    VolumeId = bookmarkDto.VolumeId,
                    SeriesId = bookmarkDto.SeriesId,
                });
            }
            else
            {
                userProgress.PagesRead = bookmarkDto.PageNum;
                userProgress.SeriesId = bookmarkDto.SeriesId;
                
            }

            _userRepository.Update(user);

            if (await _userRepository.SaveAllAsync())
            {
                return Ok();
            }
                
            
            return BadRequest("Could not save progress");
        }
    }
}