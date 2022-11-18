using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.JumpBar;
using API.DTOs.Search;
using API.DTOs.System;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Services;
using API.Services.Tasks.Scanner;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

[Authorize]
public class LibraryController : BaseApiController
{
    private readonly IDirectoryService _directoryService;
    private readonly ILogger<LibraryController> _logger;
    private readonly IMapper _mapper;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ILibraryWatcher _libraryWatcher;

    public LibraryController(IDirectoryService directoryService,
        ILogger<LibraryController> logger, IMapper mapper, ITaskScheduler taskScheduler,
        IUnitOfWork unitOfWork, IEventHub eventHub, ILibraryWatcher libraryWatcher)
    {
        _directoryService = directoryService;
        _logger = logger;
        _mapper = mapper;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _libraryWatcher = libraryWatcher;
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

        var library = new Library
        {
            Name = createLibraryDto.Name,
            Type = createLibraryDto.Type,
            Folders = createLibraryDto.Folders.Select(x => new FolderPath {Path = x}).ToList()
        };

        _unitOfWork.LibraryRepository.Add(library);

        var admins = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).ToList();
        foreach (var admin in admins)
        {
            admin.Libraries ??= new List<Library>();
            admin.Libraries.Add(library);
        }


        if (!await _unitOfWork.CommitAsync()) return BadRequest("There was a critical issue. Please try again.");

        _logger.LogInformation("Created a new library: {LibraryName}", library.Name);
        await _libraryWatcher.RestartWatching();
        _taskScheduler.ScanLibrary(library.Id);
        await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "create"), false);
        return Ok();
    }

    /// <summary>
    /// Returns a list of directories for a given path. If path is empty, returns root drives.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("list")]
    public ActionResult<IEnumerable<DirectoryDto>> GetDirectories(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Ok(Directory.GetLogicalDrives().Select(d => new DirectoryDto()
            {
                Name = d,
                FullPath = d
            }));
        }

        if (!Directory.Exists(path)) return BadRequest("This is not a valid path");

        return Ok(_directoryService.ListDirectory(path));
    }


    [HttpGet]
    public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibraries()
    {
        return Ok(await _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(User.GetUsername()));
    }

    [HttpGet("jump-bar")]
    public async Task<ActionResult<IEnumerable<JumpKeyDto>>> GetJumpBar(int libraryId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        if (!await _unitOfWork.UserRepository.HasAccessToLibrary(libraryId, userId)) return BadRequest("User does not have access to library");

        return Ok(_unitOfWork.LibraryRepository.GetJumpBarAsync(libraryId));
    }


    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("grant-access")]
    public async Task<ActionResult<MemberDto>> UpdateUserLibraries(UpdateLibraryForUserDto updateLibraryForUserDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(updateLibraryForUserDto.Username);
        if (user == null) return BadRequest("Could not validate user");

        var libraryString = string.Join(",", updateLibraryForUserDto.SelectedLibraries.Select(x => x.Name));
        _logger.LogInformation("Granting user {UserName} access to: {Libraries}", updateLibraryForUserDto.Username, libraryString);

        var allLibraries = await _unitOfWork.LibraryRepository.GetLibrariesAsync();
        foreach (var library in allLibraries)
        {
            library.AppUsers ??= new List<AppUser>();
            var libraryContainsUser = library.AppUsers.Any(u => u.UserName == user.UserName);
            var libraryIsSelected = updateLibraryForUserDto.SelectedLibraries.Any(l => l.Id == library.Id);
            if (libraryContainsUser && !libraryIsSelected)
            {
                // Remove
                library.AppUsers.Remove(user);
            }
            else if (!libraryContainsUser && libraryIsSelected)
            {
                library.AppUsers.Add(user);
            }

        }

        if (!_unitOfWork.HasChanges())
        {
            _logger.LogInformation("Added: {SelectedLibraries} to {Username}",libraryString, updateLibraryForUserDto.Username);
            return Ok(_mapper.Map<MemberDto>(user));
        }

        if (await _unitOfWork.CommitAsync())
        {
            _logger.LogInformation("Added: {SelectedLibraries} to {Username}",libraryString, updateLibraryForUserDto.Username);
            return Ok(_mapper.Map<MemberDto>(user));
        }


        return BadRequest("There was a critical issue. Please try again.");
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("scan")]
    public ActionResult Scan(int libraryId, bool force = false)
    {
        _taskScheduler.ScanLibrary(libraryId, force);
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("refresh-metadata")]
    public ActionResult RefreshMetadata(int libraryId, bool force = true)
    {
        _taskScheduler.RefreshMetadata(libraryId, force);
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("analyze")]
    public ActionResult Analyze(int libraryId)
    {
        _taskScheduler.AnalyzeFilesForLibrary(libraryId, true);
        return Ok();
    }

    /// <summary>
    /// Given a valid path, will invoke either a Scan Series or Scan Library. If the folder does not exist within Kavita, the request will be ignored
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("scan-folder")]
    public async Task<ActionResult> ScanFolder(ScanFolderDto dto)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(dto.ApiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        // Validate user has Admin privileges
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        if (!isAdmin) return BadRequest("API key must belong to an admin");

        if (dto.FolderPath.Contains("..")) return BadRequest("Invalid Path");

        dto.FolderPath = Services.Tasks.Scanner.Parser.Parser.NormalizePath(dto.FolderPath);

        var libraryFolder = (await _unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .SelectMany(l => l.Folders)
            .Distinct()
            .Select(Services.Tasks.Scanner.Parser.Parser.NormalizePath);

        var seriesFolder = _directoryService.FindHighestDirectoriesFromFiles(libraryFolder,
            new List<string>() {dto.FolderPath});

        _taskScheduler.ScanFolder(seriesFolder.Keys.Count == 1 ? seriesFolder.Keys.First() : dto.FolderPath);

        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("delete")]
    public async Task<ActionResult<bool>> DeleteLibrary(int libraryId)
    {
        var username = User.GetUsername();
        _logger.LogInformation("Library {LibraryId} is being deleted by {UserName}", libraryId, username);
        var series = await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId);
        var seriesIds = series.Select(x => x.Id).ToArray();
        var chapterIds =
            await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(seriesIds);

        try
        {
            var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.None);
            if (TaskScheduler.HasScanTaskRunningForLibrary(libraryId))
            {
                // TODO: Figure out how to cancel a job

                _logger.LogInformation("User is attempting to delete a library while a scan is in progress");
                return BadRequest(
                    "You cannot delete a library while a scan is in progress. Please wait for scan to continue then try to delete");
            }

            // Due to a bad schema that I can't figure out how to fix, we need to erase all RelatedSeries before we delete the library
            // Aka SeriesRelation has an invalid foreign key
            foreach (var s in await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(library.Id,
                         SeriesIncludes.Related))
            {
                s.Relations = new List<SeriesRelation>();
                _unitOfWork.SeriesRepository.Update(s);
            }
            await _unitOfWork.CommitAsync();

            _unitOfWork.LibraryRepository.Delete(library);

            await _unitOfWork.CommitAsync();

            if (chapterIds.Any())
            {
                await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
                await _unitOfWork.CommitAsync();
                _taskScheduler.CleanupChapters(chapterIds);
            }

            await _libraryWatcher.RestartWatching();

            foreach (var seriesId in seriesIds)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(seriesId, string.Empty, libraryId), false);
            }

            await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
                MessageFactory.LibraryModifiedEvent(libraryId, "delete"), false);
            return Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was a critical error trying to delete the library");
            await _unitOfWork.RollbackAsync();
            return Ok(false);
        }
    }

    /// <summary>
    /// Checks if the library name exists or not
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> IsLibraryNameValid(string name)
    {
        return Ok(await _unitOfWork.LibraryRepository.LibraryExists(name.Trim()));
    }

    /// <summary>
    /// Updates an existing Library with new name, folders, and/or type.
    /// </summary>
    /// <remarks>Any folder or type change will invoke a scan.</remarks>
    /// <param name="libraryForUserDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateLibrary(UpdateLibraryDto libraryForUserDto)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryForUserDto.Id, LibraryIncludes.Folders);
        if (await _unitOfWork.LibraryRepository.LibraryExists(libraryForUserDto.Name.Trim()))
            return BadRequest("Library name already exists");

        var originalFolders = library.Folders.Select(x => x.Path).ToList();

        library.Name = libraryForUserDto.Name.Trim();
        library.Folders = libraryForUserDto.Folders.Select(s => new FolderPath() {Path = s}).ToList();

        var typeUpdate = library.Type != libraryForUserDto.Type;
        library.Type = libraryForUserDto.Type;

        _unitOfWork.LibraryRepository.Update(library);

        if (!await _unitOfWork.CommitAsync()) return BadRequest("There was a critical issue updating the library.");
        if (originalFolders.Count != libraryForUserDto.Folders.Count() || typeUpdate)
        {
            await _libraryWatcher.RestartWatching();
            _taskScheduler.ScanLibrary(library.Id);
        }
        await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "update"), false);

        return Ok();

    }


    [HttpGet("type")]
    public async Task<ActionResult<LibraryType>> GetLibraryType(int libraryId)
    {
        return Ok(await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(libraryId));
    }
}
