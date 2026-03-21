using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using EasyCaching.Core;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.JumpBar;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.System;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Server.Attributes;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskScheduler = Kavita.Services.TaskScheduler;

namespace Kavita.Server.Controllers;

[Authorize]
public class LibraryController(
    IDirectoryService directoryService,
    ILogger<LibraryController> logger,
    IMapper mapper,
    ITaskScheduler taskScheduler,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    ILibraryWatcher libraryWatcher,
    IEasyCachingProviderFactory cachingProviderFactory,
    ILocalizationService localizationService)
    : BaseApiController
{
    private readonly IEasyCachingProvider _libraryCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.Library);
    private const string CacheKey = "library_";

    /// <summary>
    /// Creates a new Library. Upon library creation, adds new library to all Admin accounts.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>Created Library</returns>
    [HttpPost("create")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<LibraryDto?>> AddLibrary(UpdateLibraryDto dto)
    {
        if (await unitOfWork.LibraryRepository.LibraryExists(dto.Name))
        {
            return BadRequest(await localizationService.Translate(UserId, "library-name-exists"));
        }

        var library = new LibraryBuilder(dto.Name, dto.Type)
            .WithFolders(dto.Folders.Select(x => new FolderPath {Path = x}).Distinct().ToList())
            .WithFolderWatching(dto.FolderWatching)
            .WithIncludeInDashboard(dto.IncludeInDashboard)
            .WithManageCollections(dto.ManageCollections)
            .WithManageReadingLists(dto.ManageReadingLists)
            .WithAllowScrobbling(dto.AllowScrobbling)
            .WithAllowMetadataMatching(dto.AllowMetadataMatching)
            .WithEnableMetadata(dto.EnableMetadata)
            .Build();

        library.LibraryFileTypes = dto.FileGroupTypes
            .Select(t => new LibraryFileTypeGroup() {FileTypeGroup = t, LibraryId = library.Id})
            .Distinct()
            .ToList();
        library.LibraryExcludePatterns = dto.ExcludePatterns
            .Select(t => new LibraryExcludePattern() {Pattern = t, LibraryId = library.Id})
            .Distinct()
            .ToList();
        library.RemovePrefixForSortName = dto.RemovePrefixForSortName;
        library.DefaultLanguage = dto.DefaultLanguage;
        library.InheritWebLinksFromFirstChapter = dto.InheritWebLinksFromFirstChapter;

        // Override Scrobbling for Comic libraries since there are no providers to scrobble to
        if (library.Type == LibraryType.Comic)
        {
            logger.LogInformation("Overrode Library {Name} to disable scrobbling since there are no providers for Comics", dto.Name.Sanitize());
            library.AllowScrobbling = false;
        }

        unitOfWork.LibraryRepository.Add(library);

        var admins = (await unitOfWork.UserRepository.GetAdminUsersAsync()).ToList();
        foreach (var admin in admins)
        {
            admin.Libraries ??= new List<Library>();
            admin.Libraries.Add(library);
        }

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-library"));

        logger.LogInformation("Created a new library: {LibraryName}", library.Name.Sanitize());

        // Restart Folder watching if on
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (settings.EnableFolderWatching)
        {
            await libraryWatcher.RestartWatching();
        }

        // Assign all the necessary users with this library side nav
        var userIds = admins.Select(u => u.Id).ToList();
        var userNeedingNewLibrary = (await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.SideNavStreams))
            .Where(u => userIds.Contains(u.Id))
            .ToList();

        foreach (var user in userNeedingNewLibrary)
        {
            user.CreateSideNavFromLibrary(library);
            unitOfWork.UserRepository.Update(user);
        }

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-library"));

        // I added this twice as some users were having issues where their new library wasn't added to the side nav.
        // I wasn't able to reproduce but could validate it didn't happen with this extra commit. (https://github.com/Kareadita/Kavita/issues/4248)
        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

        if (library.FolderWatching)
        {
            await libraryWatcher.RestartWatching();
        }

        BackgroundJob.Enqueue(() => taskScheduler.ScanLibrary(library.Id, false));
        await eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "create"), false);
        await eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
            MessageFactory.SideNavUpdateEvent(UserId), false);

        return Ok(await unitOfWork.LibraryRepository.GetLibraryDtoByIdAsync(library.Id));
    }

    /// <summary>
    /// Returns a list of directories for a given path. If path is empty, returns root drives.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [HttpGet("list")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
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

        if (path.Contains("..")) return BadRequest();

        if (!Directory.Exists(path)) return Ok(directoryService.ListDirectory(Path.GetDirectoryName(path)!));

        return Ok(directoryService.ListDirectory(path));
    }

    /// <summary>
    /// For each root, checks if there are any supported files at root to warn the user during library creation about an invalid setup
    /// </summary>
    /// <returns></returns>
    [HttpPost("has-files-at-root")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult<IList<string>> AnyFilesAtRoot(CheckForFilesInFolderRootsDto dto)
    {
        var foldersWithFilesAtRoot = dto.Roots
            .Where(root => directoryService
                .GetFilesWithCertainExtensions(root, Parser.SupportedExtensions, SearchOption.TopDirectoryOnly)
                .Any())
            .ToList();

        return Ok(foldersWithFilesAtRoot);
    }

    /// <summary>
    /// Return a specific library
    /// </summary>
    /// <remarks>If the user is not an admin, only id, type, and name will be returned (<see cref="LiteLibraryDto"/>)</remarks>
    /// <returns></returns>
    [HttpGet]
    [LibraryAccess]
    public async Task<ActionResult<LibraryDto?>> GetLibrary(int libraryId)
    {
        if (User.IsInRole(PolicyConstants.AdminRole))
        {
            return Ok(await unitOfWork.LibraryRepository.GetLibraryDtoByIdAsync(libraryId));
        }
        return Ok(await unitOfWork.LibraryRepository.GetLiteLibraryDtoByIdAsync(libraryId));
    }

    /// <summary>
    /// Return all libraries in the Server
    /// </summary>
    /// <returns></returns>
    [HttpGet("libraries")]
    public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibraries()
    {
        return Ok(await GetLibrariesForUser(Username!));
    }

    /// <summary>
    /// Gets libraries for the given user that you also have access to
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("user-libraries")]
    public async Task<ActionResult<IEnumerable<LibraryDto>>> GetLibrariesForUser(int userId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.UserName)) return BadRequest();

        var ownLibraries = await GetLibrariesForUser(Username!);
        var otherLibraries = await GetLibrariesForUser(user.UserName);

        var sharedLibraries = otherLibraries.IntersectBy(ownLibraries.Select(l => l.Id), l => l.Id).ToList();

        return Ok(sharedLibraries);
    }

    /// <summary>
    /// Get all libraries a giver username has access to. And cache them for 24h
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    private async Task<IList<LibraryDto>> GetLibrariesForUser(string username)
    {
        var cacheKey = CacheKey + username;
        var result = await _libraryCacheProvider.GetAsync<IList<LibraryDto>>(cacheKey);
        if (result.HasValue) return result.Value;

        var ret = await unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(username);
        await _libraryCacheProvider.SetAsync(CacheKey, ret, TimeSpan.FromHours(24));
        return ret;
    }

    /// <summary>
    /// For a given library, generate the jump bar information
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [LibraryAccess]
    [HttpGet("jump-bar")]
    public ActionResult<IEnumerable<JumpKeyDto>> GetJumpBar(int libraryId)
    {
        return Ok(unitOfWork.LibraryRepository.GetJumpBarAsync(libraryId));
    }

    /// <summary>
    /// Grants a user account access to a Library
    /// </summary>
    /// <param name="updateLibraryForUserDto"></param>
    /// <returns></returns>
    [HttpPost("grant-access")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<MemberDto>> UpdateUserLibraries(UpdateLibraryForUserDto updateLibraryForUserDto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(updateLibraryForUserDto.Username, AppUserIncludes.SideNavStreams);
        if (user == null) return BadRequest(await localizationService.Translate(UserId, "user-doesnt-exist"));

        var libraryString = string.Join(',', updateLibraryForUserDto.SelectedLibraries.Select(x => x.Name));
        logger.LogInformation("Granting user {UserId} access to: {Libraries}", user.Id, libraryString.Sanitize());

        var allLibraries = await unitOfWork.LibraryRepository.GetLibrariesAsync();
        foreach (var library in allLibraries)
        {
            library.AppUsers ??= [];

            var libraryContainsUser = library.AppUsers.Any(u => u.UserName == user.UserName);
            var libraryIsSelected = updateLibraryForUserDto.SelectedLibraries.Any(l => l.Id == library.Id);

            if (libraryContainsUser && !libraryIsSelected)
            {
                library.AppUsers.Remove(user);
                user.RemoveSideNavFromLibrary(library);
            }
            else if (!libraryContainsUser && libraryIsSelected)
            {
                library.AppUsers.Add(user);
                user.CreateSideNavFromLibrary(library);
            }
        }

        if (!unitOfWork.HasChanges())
        {
            logger.LogInformation("No changes for update library access");
            return Ok(mapper.Map<MemberDto>(user));
        }

        if (await unitOfWork.CommitAsync())
        {
            logger.LogInformation("Added: {SelectedLibraries} to {UserId}", libraryString.Sanitize(), user.Id);
            // Bust cache
            await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

            unitOfWork.UserRepository.Update(user);

            return Ok(mapper.Map<MemberDto>(user));
        }


        return BadRequest(await localizationService.Translate(UserId, "generic-library"));
    }

    /// <summary>
    /// Scans a given library for file changes.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="force">If true, will ignore any optimizations to avoid file I/O and will treat similar to a first scan</param>
    /// <returns></returns>
    [HttpPost("scan")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> Scan(int libraryId, bool force = false)
    {
        if (libraryId <= 0) return BadRequest(await localizationService.Translate(UserId, "greater-0", "libraryId"));
        await taskScheduler.ScanLibrary(libraryId, force);
        return Ok();
    }

    /// <summary>
    /// Enqueues a bunch of library scans
    /// </summary>
    /// <returns></returns>
    [HttpPost("scan-multiple")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> ScanMultiple(BulkActionDto dto)
    {
        foreach (var libraryId in dto.Ids)
        {
            await taskScheduler.ScanLibrary(libraryId, dto.Force ?? false);
        }

        return Ok();
    }

    /// <summary>
    /// Scans a given library for file changes. If another scan task is in progress, will reschedule the invocation for 3 hours in future.
    /// </summary>
    /// <param name="force">If true, will ignore any optimizations to avoid file I/O and will treat similar to a first scan</param>
    /// <returns></returns>
    [HttpPost("scan-all")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult ScanAll(bool force = false)
    {
        taskScheduler.ScanLibraries(force);
        return Ok();
    }

    [HttpPost("refresh-metadata")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult RefreshMetadata(int libraryId, bool force = true, bool forceColorscape = true)
    {
        taskScheduler.RefreshMetadata(libraryId, force, forceColorscape);
        return Ok();
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("refresh-metadata-multiple")]
    public ActionResult RefreshMetadataMultiple(BulkActionDto dto, bool forceColorscape = true)
    {
        foreach (var libraryId in dto.Ids)
        {
            taskScheduler.RefreshMetadata(libraryId, dto.Force ?? false, forceColorscape);
        }

        return Ok();
    }

    /// <summary>
    /// Copy the library settings (adv tab + optional type) to a set of other libraries.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("copy-settings-from")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> CopySettingsFromLibraryToLibraries(CopySettingsFromLibraryDto dto)
    {
        var sourceLibrary = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(dto.SourceLibraryId, LibraryIncludes.ExcludePatterns | LibraryIncludes.FileTypes);
        if (sourceLibrary == null) return BadRequest("SourceLibraryId must exist");

        var libraries = await unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.TargetLibraryIds, LibraryIncludes.ExcludePatterns | LibraryIncludes.FileTypes | LibraryIncludes.Folders);
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

        await unitOfWork.CommitAsync();

        if (sourceLibrary.FolderWatching)
        {
            BackgroundJob.Enqueue(() => libraryWatcher.RestartWatching());
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
        var user = await unitOfWork.UserRepository.GetUserByAuthKey(dto.ApiKey);
        if (user == null) return Unauthorized();

        // Validate user has Admin privileges
        var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user);
        if (!isAdmin) return BadRequest("API key must belong to an admin");

        if (dto.FolderPath.Contains(".."))
        {
            return BadRequest(await localizationService.Translate(UserId, "invalid-path"));
        }

        dto.FolderPath = Parser.NormalizePath(dto.FolderPath);

        var libraryFolder = (await unitOfWork.LibraryRepository.GetLibraryDtosAsync())
            .SelectMany(l => l.Folders)
            .Distinct()
            .Select(Parser.NormalizePath);

        var seriesFolder = directoryService.FindHighestDirectoriesFromFiles(libraryFolder, [dto.FolderPath]);

        taskScheduler.ScanFolder(seriesFolder.Keys.Count == 1 ? seriesFolder.Keys.First() : dto.FolderPath, dto.AbortOnNoSeriesMatch);

        return Ok();
    }

    /// <summary>
    /// Deletes the library and all series within it.
    /// </summary>
    /// <remarks>This does not touch any files</remarks>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpDelete("delete")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> DeleteLibrary(int libraryId)
    {
        logger.LogInformation("Library {LibraryId} is being deleted by {UserName}", libraryId, Username!);

        try
        {
            var result = await DeleteLibrary(libraryId, UserId);
            if (result)
            {
                // Inform the user's side nav to remove it if needed
                await eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
                    MessageFactory.SideNavUpdateEvent(UserId), false);
            }
            return Ok(result);
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
    [HttpDelete("delete-multiple")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> DeleteMultipleLibraries([FromQuery] List<int> libraryIds)
    {
        var username = Username!;

        var allLibraries = await unitOfWork.LibraryRepository.GetLibrariesAsync();
        var toDelete = allLibraries.Where(l => libraryIds.Contains(l.Id)).Select(l => l.Id).ToList();

        logger.LogInformation("Libraries {LibraryIds} are being deleted by {UserName}", toDelete, username);

        foreach (var libraryId in toDelete)
        {
            try
            {
                await DeleteLibrary(libraryId, UserId);
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
        var series = await unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId);
        var seriesIds = series.Select(x => x.Id).ToArray();
        var chapterIds =
            await unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(seriesIds);

        try
        {
            if (TaskScheduler.HasScanTaskRunningForLibrary(libraryId))
            {
                logger.LogInformation("User is attempting to delete a library while a scan is in progress");
                throw new KavitaException(await localizationService.Translate(userId, "delete-library-while-scan"));
            }

            var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
            if (library == null)
            {
                throw new KavitaException(await localizationService.Translate(userId, "library-doesnt-exist"));
            }


            // Due to a bad schema that I can't figure out how to fix, we need to erase all RelatedSeries before we delete the library
            // Aka SeriesRelation has an invalid foreign key
            foreach (var s in await unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(library.Id, SeriesIncludes.Related))
            {
                s.Relations = new List<SeriesRelation>();
                unitOfWork.SeriesRepository.Update(s);
            }
            await unitOfWork.CommitAsync();

            unitOfWork.LibraryRepository.Delete(library);

            var streams = await unitOfWork.UserRepository.GetSideNavStreamsByLibraryId(library.Id);
            unitOfWork.UserRepository.Delete(streams);


            await unitOfWork.CommitAsync();

            await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);
            await eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
                MessageFactory.SideNavUpdateEvent(userId), false);

            if (chapterIds.Any())
            {
                await unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
                await unitOfWork.CommitAsync();
                taskScheduler.CleanupChapters(chapterIds);
            }

            BackgroundJob.Enqueue(() => libraryWatcher.RestartWatching());

            foreach (var seriesId in seriesIds)
            {
                await eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(seriesId, string.Empty, libraryId), false);
            }

            await eventHub.SendMessageAsync(MessageFactory.LibraryModified,
                MessageFactory.LibraryModifiedEvent(libraryId, "delete"), false);

            var userPreferences = await unitOfWork.DataContext.AppUserPreferences.ToListAsync();
            foreach (var userPreference in userPreferences)
            {
                userPreference.SocialPreferences.SocialLibraries = userPreference.SocialPreferences.SocialLibraries
                    .Where(l => l != libraryId).ToList();
            }

            await unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was a critical issue. Please try again");
            await unitOfWork.RollbackAsync();
            return false;
        }
    }

    /// <summary>
    /// Checks if the library name exists or not
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [HttpGet("name-exists")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> IsLibraryNameValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(true);
        return Ok(await unitOfWork.LibraryRepository.LibraryExists(name.Trim()));
    }

    /// <summary>
    /// Updates an existing Library with new name, folders, and/or type.
    /// </summary>
    /// <remarks>Any folder or type change will invoke a scan.</remarks>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateLibrary(UpdateLibraryDto dto)
    {
        var userId = UserId;
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(dto.Id, LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        if (library == null) return BadRequest(await localizationService.Translate(userId, "library-doesnt-exist"));

        var newName = dto.Name.Trim();
        if (await unitOfWork.LibraryRepository.LibraryExists(newName) && !library.Name.Equals(newName))
            return BadRequest(await localizationService.Translate(userId, "library-name-exists"));

        var originalFoldersCount = library.Folders.Count;

        library.Name = newName;
        library.Folders = dto.Folders.Select(s => new FolderPath() {Path = s}).Distinct().ToList();

        var typeUpdate = library.Type != dto.Type;
        var folderWatchingUpdate = library.FolderWatching != dto.FolderWatching;
        UpdateLibrarySettings(dto, library);

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(userId, "generic-library-update"));

        if (folderWatchingUpdate || originalFoldersCount != dto.Folders.Count() || typeUpdate)
        {
            BackgroundJob.Enqueue(() => libraryWatcher.RestartWatching());
        }

        if (originalFoldersCount != dto.Folders.Count() || typeUpdate)
        {
            await taskScheduler.ScanLibrary(library.Id);
        }

        await eventHub.SendMessageAsync(MessageFactory.LibraryModified,
            MessageFactory.LibraryModifiedEvent(library.Id, "update"), false);

        await eventHub.SendMessageAsync(MessageFactory.SideNavUpdate,
            MessageFactory.SideNavUpdateEvent(userId), false);

        await _libraryCacheProvider.RemoveByPrefixAsync(CacheKey);

        return Ok(await unitOfWork.LibraryRepository.GetLibraryDtoByIdAsync(library.Id));

    }

    private void UpdateLibrarySettings(UpdateLibraryDto dto, Library library, bool updateType = true)
    {
        // Reminder: Add new fields to the Create Library Endpoint!

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
        library.EnableMetadata = dto.EnableMetadata;
        library.RemovePrefixForSortName = dto.RemovePrefixForSortName;
        library.InheritWebLinksFromFirstChapter = dto.InheritWebLinksFromFirstChapter;
        library.DefaultLanguage = dto.DefaultLanguage;

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
            logger.LogInformation("Overrode Library {Name} to disable scrobbling since there are no providers for Comics", dto.Name.Replace(Environment.NewLine, string.Empty));
            library.AllowScrobbling = false;
        }


        unitOfWork.LibraryRepository.Update(library);
    }

    /// <summary>
    /// Returns the type of the underlying library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [LibraryAccess]
    [HttpGet("type")]
    public async Task<ActionResult<LibraryType>> GetLibraryType(int libraryId)
    {
        return Ok(await unitOfWork.LibraryRepository.GetLibraryTypeAsync(libraryId));
    }
}
