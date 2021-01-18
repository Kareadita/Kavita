using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize]
    public class LibraryController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ILogger<LibraryController> _logger;
        private readonly IMapper _mapper;
        private readonly ITaskScheduler _taskScheduler;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public LibraryController(IDirectoryService directoryService, 
            ILogger<LibraryController> logger, IMapper mapper, ITaskScheduler taskScheduler, 
            IUnitOfWork unitOfWork, ICacheService cacheService)
        {
            _directoryService = directoryService;
            _logger = logger;
            _mapper = mapper;
            _taskScheduler = taskScheduler;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }
        
        /// <summary>
        /// Creates a new Library. Upon library creation, adds new library to all Admin accounts.
        /// </summary>
        /// <param name="createLibraryDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("create")]
        public async Task<ActionResult> AddLibrary(CreateLibraryDto createLibraryDto)
        {
            if (await _unitOfWork.LibraryRepository.LibraryExists(createLibraryDto.Name))
            {
                return BadRequest("Library name already exists. Please choose a unique name to the server.");
            }

            var admins = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).ToList();
            
            var library = new Library
            {
                Name = createLibraryDto.Name,
                Type = createLibraryDto.Type,
                AppUsers = admins,
                Folders = createLibraryDto.Folders.Select(x => new FolderPath {Path = x}).ToList()
            };

            foreach (var admin in admins)
            {
                // If user is null, then set it
                admin.Libraries ??= new List<Library>();
                admin.Libraries.Add(library);
            }


            if (!await _unitOfWork.Complete())
            {
                return BadRequest("There was a critical issue. Please try again.");
            }
            
            _logger.LogInformation($"Created a new library: {library.Name}");
            _taskScheduler.ScanLibrary(library.Id);
            return Ok();
        }

        /// <summary>
        /// Returns a list of directories for a given path. If path is empty, returns root drives.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("list")]
        public ActionResult<IEnumerable<string>> GetDirectories(string path)
        {
            // TODO: Move this to another controller. 
            if (string.IsNullOrEmpty(path))
            {
                return Ok(Directory.GetLogicalDrives());
            }

            if (!Directory.Exists(path)) return BadRequest("This is not a valid path");

            return Ok(_directoryService.ListDirectory(path));
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibraries()
        {
            return Ok(await _unitOfWork.LibraryRepository.GetLibraryDtosAsync());
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPut("grant-access")]
        public async Task<ActionResult<MemberDto>> AddLibraryToUser(UpdateLibraryForUserDto updateLibraryForUserDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(updateLibraryForUserDto.Username);

            if (user == null) return BadRequest("Could not validate user");

            user.Libraries = new List<Library>();

            foreach (var selectedLibrary in updateLibraryForUserDto.SelectedLibraries)
            {
                user.Libraries.Add(_mapper.Map<Library>(selectedLibrary));
            }
            
            if (await _unitOfWork.Complete())
            {
                _logger.LogInformation($"Added: {updateLibraryForUserDto.SelectedLibraries} to {updateLibraryForUserDto.Username}");
                return Ok(user);
            }

            return BadRequest("There was a critical issue. Please try again.");
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("scan")]
        public ActionResult Scan(int libraryId)
        {
            //BackgroundJob.Enqueue(() => _directoryService.ScanLibrary(libraryId, true));
            _taskScheduler.ScanLibrary(libraryId, true);
            return Ok();
        }

        [HttpGet("libraries-for")]
        public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibrariesForUser(string username)
        {
            return Ok(await _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(username));
        }

        [HttpGet("series")]
        public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibrary(int libraryId, bool forUser = false)
        {
            // TODO: Move to series? 
            if (forUser)
            {
                var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
                return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, user.Id));
            }
            return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId));
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("delete")]
        public async Task<ActionResult<bool>> DeleteLibrary(int libraryId)
        {
            var username = User.GetUsername();
            _logger.LogInformation($"Library {libraryId} is being deleted by {username}.");
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId);
            var volumes = (await _unitOfWork.SeriesRepository.GetVolumesForSeriesAsync(series.Select(x => x.Id).ToArray()))
                                .Select(x => x.Id).ToArray();
            var result = await _unitOfWork.LibraryRepository.DeleteLibrary(libraryId);
            
            if (result && volumes.Any())
            {
                _taskScheduler.CleanupVolumes(volumes);
            }
            
            return Ok(result);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("update")]
        public async Task<ActionResult> UpdateLibrary(UpdateLibraryDto libraryForUserDto)
        {
            var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryForUserDto.Id);

            var originalFolders = library.Folders.Select(x => x.Path);
            var differenceBetweenFolders = originalFolders.Except(libraryForUserDto.Folders);

            library.Name = libraryForUserDto.Name;
            library.Folders = libraryForUserDto.Folders.Select(s => new FolderPath() {Path = s}).ToList();

            _unitOfWork.LibraryRepository.Update(library);

            if (!await _unitOfWork.Complete()) return BadRequest("There was a critical issue updating the library.");
            if (differenceBetweenFolders.Any())
            {
                _taskScheduler.ScanLibrary(library.Id, true);
            }
                
            return Ok();

        }
    }
}