using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
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
        private readonly ILibraryRepository _libraryRepository;
        private readonly ILogger<LibraryController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ITaskScheduler _taskScheduler;
        private readonly ISeriesRepository _seriesRepository;

        public LibraryController(IDirectoryService directoryService, 
            ILibraryRepository libraryRepository, ILogger<LibraryController> logger, IUserRepository userRepository,
            IMapper mapper, ITaskScheduler taskScheduler, ISeriesRepository seriesRepository)
        {
            _directoryService = directoryService;
            _libraryRepository = libraryRepository;
            _logger = logger;
            _userRepository = userRepository;
            _mapper = mapper;
            _taskScheduler = taskScheduler;
            _seriesRepository = seriesRepository;
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
            return Ok(await _libraryRepository.GetLibrariesAsync());
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPut("update-for")]
        public async Task<ActionResult<MemberDto>> UpdateLibrary(UpdateLibraryDto updateLibraryDto)
        {
            var user = await _userRepository.GetUserByUsernameAsync(updateLibraryDto.Username);

            if (user == null) return BadRequest("Could not validate user");

            user.Libraries = new List<Library>();

            foreach (var selectedLibrary in updateLibraryDto.SelectedLibraries)
            {
                user.Libraries.Add(_mapper.Map<Library>(selectedLibrary));
            }
            
            if (await _userRepository.SaveAllAsync())
            {
                return Ok(user);
            }

            return BadRequest("There was a critical issue. Please try again.");
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("scan")]
        public async Task<ActionResult> ScanLibrary(int libraryId)
        {
            var library = await _libraryRepository.GetLibraryDtoForIdAsync(libraryId);
            
            // We have to send a json encoded Library (aka a DTO) to the Background Job thread. 
            // Because we use EF, we have circular dependencies back to Library and it will crap out
            BackgroundJob.Enqueue(() => _directoryService.ScanLibrary(library));
            return Ok();
        }

        [HttpGet("libraries-for")]
        public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibrariesForUser(string username)
        {
            return Ok(await _libraryRepository.GetLibrariesForUsernameAysnc(username));
        }

        [HttpGet("series")]
        public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibrary(int libraryId)
        {
            return Ok(await _seriesRepository.GetSeriesForLibraryIdAsync(libraryId));

        }
    }
}