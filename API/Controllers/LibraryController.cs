using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.JumpBar;
using API.DTOs.System;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner;
using API.SignalR;
using AutoMapper;
using EasyCaching.Core;
using Hangfire;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

#nullable enable

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
    private readonly ILocalizationService _localizationService;
    private readonly IEasyCachingProvider _libraryCacheProvider;
    private const string CacheKey = "library_";

    public LibraryController(IDirectoryService directoryService,
        ILogger<LibraryController> logger, IMapper mapper, ITaskScheduler taskScheduler,
        IUnitOfWork unitOfWork, IEventHub eventHub, ILibraryWatcher libraryWatcher,
        IEasyCachingProviderFactory cachingProviderFactory, ILocalizationService localizationService)
    {
        _directoryService = directoryService;
        _logger = logger;
        _mapper = mapper;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _libraryWatcher = libraryWatcher;
        _localizationService = localizationService;

        _libraryCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.Library);
    }

    /// <summary>
    /// Creates a new Library. Upon library creation, adds new library to all Admin accounts.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("create")]
    public async Task<ActionResult> AddLibrary(UpdateLibraryDto dto)
    {
        if (await _unitOfWork.LibraryRepository.LibraryExists(dto.Name))
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "library-name-exists"));
        }

        var library = new LibraryBuilder(dto.Name, dto.Type)
            .WithFolders(dto.Folders.Select(x => new FolderPath {Path = x}).Distinct().ToList())
            .WithFolderWatching(dto.FolderWatching)
            .WithIncludeInDashboard(dto.IncludeInDashboard)
            .WithManageCollections(dto.ManageCollections)
            .WithManageReadingLists(dto.ManageReadingLists)
            .WithAllowScrobbling(dto.AllowScrobbling)
            .WithAllowMetadataMatching(dto.AllowMetadataMatching)
            .Build();

        library.LibraryFileTypes = dto.FileGroupTypes
            .Select(t => new LibraryFileTypeGroup() {FileTypeGroup = t, LibraryId = library.Id})
            .Distinct()
            .ToList();
        library.LibraryExcludePatterns = dto.ExcludePatterns
            .Select(t => new LibraryExcludePattern() {Pattern = t, LibraryId = library.Id})
            .Distinct()
            .ToList();

        // Override Scrobbling for Comic libraries since there are no providers to scrobble to
        if (library.Type == LibraryType.Comic)
        {
            _logger.LogInformation("Overrode Library {Name} to disable scrobbling since there are no providers for Comics", dto.Name);
            library.AllowScrobbling = false;
        }

        _unitOfWork.LibraryRepository.Add(library);

        var admins = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).ToList();
        foreach (var admin in admins)
        {
            admin.Libraries ??= new List<Library>();
            admin.Libraries.Add(library);
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-library"));
        _logger.LogInformation("Created a new library: {LibraryName}", library.Name);

        // Restart Folder watching if on
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (settings.EnableFolderWatching)
        {
            await _libraryWatcher.RestartWatching();
        }

        // Assign all the necessary users with this library side nav
        var userIds = admins.Select(u => u.Id).Append(User.GetUserId()).ToList();
        var userNeedingNewLibrary = (await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.SideNavStreams))
            .Where(u => userIds.Contains(u.Id))
            .ToList();

        foreach (var user in userNeedingNewLibrary)
        {
            user.CreateSideNavFromLibrary(library);
            _unitOfWork.UserRepository.Update(user);
        }

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-library"));

        await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

        if (library.FolderWatching)
        {
            await _libraryWatcher.RestartWatching();
        }

        BackgroundJob.Enqueue(() => _taskScheduler.ScanLibrary(library.Id, false));
        await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "create"), false);
        await _eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
            MessageFactory.SideNavUpdateEvent(User.GetUserId()), false);

        return Ok();
    }

    /// <summary>
    /// Returns a list of directories for a given path. If path is empty, returns root drives.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("list")]
    public ActionResult<IEnumerable<DirectoryDto>> GetDirectories(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Ok(Directory.GetLogicalDrives().Select(d => new DirectoryDto()
            {
                Name = d,
                FullPath = d
            }));
        }

        if (!Directory.Exists(path)) return Ok(_directoryService.ListDirectory(Path.GetDirectoryName(path)!));

        return Ok(_directoryService.ListDirectory(path));
    }

    /// <summary>
    /// Return a specific library
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet]
    public async Task<ActionResult<LibraryDto?>> GetLibrary(int libraryId)
    {
        var username = User.GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var cacheKey = CacheKey + username;
        var result = await _libraryCacheProvider.GetAsync<IEnumerable<LibraryDto>>(cacheKey);
        if (result.HasValue)
        {
            return Ok(result.Value.FirstOrDefault(l => l.Id == libraryId));
        }

        var ret = _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(username).ToList();
        await _libraryCacheProvider.SetAsync(CacheKey, ret, TimeSpan.FromHours(24));
        _logger.LogDebug("Caching libraries for {Key}", cacheKey);

        return Ok(ret.Find(l => l.Id == libraryId));
    }

    /// <summary>
    /// Return all libraries in the Server
    /// </summary>
    /// <returns></returns>
    [HttpGet("libraries")]
    public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibraries()
    {
        var username = User.GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var cacheKey = CacheKey + username;
        var result = await _libraryCacheProvider.GetAsync<IEnumerable<LibraryDto>>(cacheKey);
        if (result.HasValue) return Ok(result.Value);

        var ret = _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(username);
        await _libraryCacheProvider.SetAsync(CacheKey, ret, TimeSpan.FromHours(24));
        _logger.LogDebug("Caching libraries for {Key}", cacheKey);

        return Ok(ret);
    }

    /// <summary>
    /// For a given library, generate the jump bar information
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("jump-bar")]
    public async Task<ActionResult<IEnumerable<JumpKeyDto>>> GetJumpBar(int libraryId)
    {
        if (!await _unitOfWork.UserRepository.HasAccessToLibrary(libraryId, User.GetUserId()))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-library-access"));

        return Ok(_unitOfWork.LibraryRepository.GetJumpBarAsync(libraryId));
    }

    /// <summary>
    /// Grants a user account access to a Library
    /// </summary>
    /// <param name="updateLibraryForUserDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("grant-access")]
    public async Task<ActionResult<MemberDto>> UpdateUserLibraries(UpdateLibraryForUserDto updateLibraryForUserDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(updateLibraryForUserDto.Username, AppUserIncludes.SideNavStreams);
        if (user == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "user-doesnt-exist"));

        var libraryString = string.Join(',', updateLibraryForUserDto.SelectedLibraries.Select(x => x.Name));
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
                user.RemoveSideNavFromLibrary(library);
            }
            else if (!libraryContainsUser && libraryIsSelected)
            {
                library.AppUsers.Add(user);
                user.CreateSideNavFromLibrary(library);
            }
        }

        if (!_unitOfWork.HasChanges())
        {
            _logger.LogInformation("No changes for update library access");
            return Ok(_mapper.Map<MemberDto>(user));
        }

        if (await _unitOfWork.CommitAsync())
        {
            _logger.LogInformation("Added: {SelectedLibraries} to {Username}",libraryString, updateLibraryForUserDto.Username);
            // Bust cache
            await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

            _unitOfWork.UserRepository.Update(user);

            return Ok(_mapper.Map<MemberDto>(user));
        }


        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-library"));
    }

    /// <summary>
    /// Scans a given library for file changes.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="force">If true, will ignore any optimizations to avoid file I/O and will treat similar to a first scan</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("scan")]
    public async Task<ActionResult> Scan(int libraryId, bool force = false)
    {
        if (libraryId <= 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "libraryId"));
        await _taskScheduler.ScanLibrary(libraryId, force);
        return Ok();
    }

    /// <summary>
    /// Enqueues a bunch of library scans
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("scan-multiple")]
    public async Task<ActionResult> ScanMultiple(BulkActionDto dto)
    {
        foreach (var libraryId in dto.Ids)
        {
            await _taskScheduler.ScanLibrary(libraryId, dto.Force ?? false);
        }

        return Ok();
    }

    /// <summary>
    /// Scans a given library for file changes. If another scan task is in progress, will reschedule the invocation for 3 hours in future.
    /// </summary>
    /// <param name="force">If true, will ignore any optimizations to avoid file I/O and will treat similar to a first scan</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("scan-all")]
    public ActionResult ScanAll(bool force = false)
    {
        _taskScheduler.ScanLibraries(force);
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("refresh-metadata")]
    public ActionResult RefreshMetadata(int libraryId, bool force = true, bool forceColorscape = true)
    {
        _taskScheduler.RefreshMetadata(libraryId, force, forceColorscape);
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("refresh-metadata-multiple")]
    public ActionResult RefreshMetadataMultiple(BulkActionDto dto, bool forceColorscape = true)
    {
        foreach (var libraryId in dto.Ids)
        {
            _taskScheduler.RefreshMetadata(libraryId, dto.Force ?? false, forceColorscape);
        }

        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("analyze")]
    public ActionResult Analyze(int libraryId)
    {
        _taskScheduler.AnalyzeFilesForLibrary(libraryId, true);
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("analyze-multiple")]
    public ActionResult AnalyzeMultiple(BulkActionDto dto)
    {
        foreach (var libraryId in dto.Ids)
        {
            _taskScheduler.AnalyzeFilesForLibrary(libraryId, dto.Force ?? false);
        }

        return Ok();
    }


    /// <summary>
    /// Copy the library settings (adv tab + optional type) to a set of other libraries.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("copy-settings-from")]
    public async Task<ActionResult> CopySettingsFromLibraryToLibraries(CopySettingsFromLibraryDto dto)
    {
        var sourceLibrary = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(dto.SourceLibraryId, LibraryIncludes.ExcludePatterns | LibraryIncludes.FileTypes);
        if (sourceLibrary == null) return BadRequest("SourceLibraryId must exist");

        var libraries = await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.TargetLibraryIds, LibraryIncludes.ExcludePatterns | LibraryIncludes.FileTypes | LibraryIncludes.Folders);
        foreach (var targetLibrary in libraries)
        {
            UpdateLibrarySettings(new UpdateLibraryDto()
            {
                Folders = targetLibrary.Folders.Select(s => s.Path),
                Name = targetLibrary.Name,
                Id = targetLibrary.Id,
                Type = sourceLibrary.Type,
                AllowScrobbling = sourceLibrary.AllowScrobbling,
                ExcludePatterns = sourceLibrary.LibraryExcludePatterns.Select(p => p.Pattern).ToList(),
                FolderWatching = sourceLibrary.FolderWatching,
                ManageCollections = sourceLibrary.ManageCollections,
                FileGroupTypes = sourceLibrary.LibraryFileTypes.Select(t => t.FileTypeGroup).ToList(),
                IncludeInDashboard = sourceLibrary.IncludeInDashboard,
                IncludeInSearch = sourceLibrary.IncludeInSearch,
                ManageReadingLists = sourceLibrary.ManageReadingLists
            }, targetLibrary, dto.IncludeType);
        }

        await _unitOfWork.CommitAsync();

        if (sourceLibrary.FolderWatching)
        {
            BackgroundJob.Enqueue(() => _libraryWatcher.RestartWatching());
        }

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
        if (user == null) return Unauthorized();

        // Validate user has Admin privileges
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        if (!isAdmin) return BadRequest("API key must belong to an admin");

        if (dto.FolderPath.Contains("..")) return BadRequest(await _localizationService.Translate(user.Id, "invalid-path"));

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

    /// <summary>
    /// Deletes the library and all series within it.
    /// </summary>
    /// <remarks>This does not touch any files</remarks>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("delete")]
    public async Task<ActionResult<bool>> DeleteLibrary(int libraryId)
    {
        _logger.LogInformation("Library {LibraryId} is being deleted by {UserName}", libraryId, User.GetUsername());

        try
        {
            return Ok(await DeleteLibrary(libraryId, User.GetUserId()));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Deletes multiple libraries and all series within it.
    /// </summary>
    /// <remarks>This does not touch any files</remarks>
    /// <param name="libraryIds"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("delete-multiple")]
    public async Task<ActionResult<bool>> DeleteMultipleLibraries([FromQuery] List<int> libraryIds)
    {
        var username = User.GetUsername();
        _logger.LogInformation("Libraries {LibraryIds} are being deleted by {UserName}", libraryIds, username);

        foreach (var libraryId in libraryIds)
        {
            try
            {
                await DeleteLibrary(libraryId, User.GetUserId());
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        return Ok();
    }

    private async Task<bool> DeleteLibrary(int libraryId, int userId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId);
        var seriesIds = series.Select(x => x.Id).ToArray();
        var chapterIds =
            await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(seriesIds);

        try
        {
            if (TaskScheduler.HasScanTaskRunningForLibrary(libraryId))
            {
                _logger.LogInformation("User is attempting to delete a library while a scan is in progress");
                throw new KavitaException(await _localizationService.Translate(userId, "delete-library-while-scan"));
            }

            var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
            if (library == null)
            {
                throw new KavitaException(await _localizationService.Translate(userId, "library-doesnt-exist"));
            }


            // Due to a bad schema that I can't figure out how to fix, we need to erase all RelatedSeries before we delete the library
            // Aka SeriesRelation has an invalid foreign key
            foreach (var s in await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(library.Id, SeriesIncludes.Related))
            {
                s.Relations = new List<SeriesRelation>();
                _unitOfWork.SeriesRepository.Update(s);
            }
            await _unitOfWork.CommitAsync();

            _unitOfWork.LibraryRepository.Delete(library);

            var streams = await _unitOfWork.UserRepository.GetSideNavStreamsByLibraryId(library.Id);
            _unitOfWork.UserRepository.Delete(streams);


            await _unitOfWork.CommitAsync();

            await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);
            await _eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
                MessageFactory.SideNavUpdateEvent(userId), false);

            if (chapterIds.Any())
            {
                await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
                await _unitOfWork.CommitAsync();
                _taskScheduler.CleanupChapters(chapterIds);
            }

            BackgroundJob.Enqueue(() => _libraryWatcher.RestartWatching());

            foreach (var seriesId in seriesIds)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(seriesId, string.Empty, libraryId), false);
            }

            await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
                MessageFactory.LibraryModifiedEvent(libraryId, "delete"), false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was a critical issue. Please try again");
            await _unitOfWork.RollbackAsync();
            return false;
        }
    }

    /// <summary>
    /// Checks if the library name exists or not
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> IsLibraryNameValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(true);
        return Ok(await _unitOfWork.LibraryRepository.LibraryExists(name.Trim()));
    }

    /// <summary>
    /// Updates an existing Library with new name, folders, and/or type.
    /// </summary>
    /// <remarks>Any folder or type change will invoke a scan.</remarks>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateLibrary(UpdateLibraryDto dto)
    {
        var userId = User.GetUserId();
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(dto.Id, LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        if (library == null) return BadRequest(await _localizationService.Translate(userId, "library-doesnt-exist"));

        var newName = dto.Name.Trim();
        if (await _unitOfWork.LibraryRepository.LibraryExists(newName) && !library.Name.Equals(newName))
            return BadRequest(await _localizationService.Translate(userId, "library-name-exists"));

        var originalFoldersCount = library.Folders.Count;

        library.Name = newName;
        library.Folders = dto.Folders.Select(s => new FolderPath() {Path = s}).Distinct().ToList();

        var typeUpdate = library.Type != dto.Type;
        var folderWatchingUpdate = library.FolderWatching != dto.FolderWatching;
        UpdateLibrarySettings(dto, library);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(userId, "generic-library-update"));

        if (folderWatchingUpdate || originalFoldersCount != dto.Folders.Count() || typeUpdate)
        {
            BackgroundJob.Enqueue(() => _libraryWatcher.RestartWatching());
        }

        if (originalFoldersCount != dto.Folders.Count() || typeUpdate)
        {
            await _taskScheduler.ScanLibrary(library.Id);
        }

        await _eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "update"), false);

        await _eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
            MessageFactory.SideNavUpdateEvent(userId), false);

        await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

        return Ok();

    }

    private void UpdateLibrarySettings(UpdateLibraryDto dto, Library library, bool updateType = true)
    {
        if (updateType)
        {
            library.Type = dto.Type;
        }

        library.FolderWatching = dto.FolderWatching;
        library.IncludeInDashboard = dto.IncludeInDashboard;
        library.IncludeInSearch = dto.IncludeInSearch;
        library.ManageCollections = dto.ManageCollections;
        library.ManageReadingLists = dto.ManageReadingLists;
        library.AllowScrobbling = dto.AllowScrobbling;
        library.AllowMetadataMatching = dto.AllowMetadataMatching;
        library.LibraryFileTypes = dto.FileGroupTypes
            .Select(t => new LibraryFileTypeGroup() {FileTypeGroup = t, LibraryId = library.Id})
            .Distinct()
            .ToList();

        library.LibraryExcludePatterns = dto.ExcludePatterns
            .Distinct()
            .Select(t => new LibraryExcludePattern() {Pattern = t, LibraryId = library.Id})
            .ToList();

        // Override Scrobbling for Comic libraries since there are no providers to scrobble to
        if (library.Type is LibraryType.Comic or LibraryType.ComicVine)
        {
            _logger.LogInformation("Overrode Library {Name} to disable scrobbling since there are no providers for Comics", dto.Name.Replace(Environment.NewLine, string.Empty));
            library.AllowScrobbling = false;
        }


        _unitOfWork.LibraryRepository.Update(library);
    }

    /// <summary>
    /// Returns the type of the underlying library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("type")]
    public async Task<ActionResult<LibraryType>> GetLibraryType(int libraryId)
    {
        return Ok(await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(libraryId));
    }
}
