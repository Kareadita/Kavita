using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.KavitaPlus.Account;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.SignalR;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

#nullable enable

[Authorize]
public class UsersController(
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IEventHub eventHub,
    ILocalizationService localizationService,
    IScrobblingService scrobblingService)
    : BaseApiController
{
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpDelete("delete-user")]
    public async Task<ActionResult> DeleteUser(int userId)
    {
        var ct = HttpContext.RequestAborted;
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) return NotFound();

        // Remove all likes for the user, so like counts are correct
        var annotations = await unitOfWork.AnnotationRepository.GetAllAnnotations(ct);
        foreach (var annotation in annotations.Where(a => a.Likes.Contains(user.Id)))
        {
            annotation.Likes.Remove(user.Id);
            unitOfWork.AnnotationRepository.Update(annotation);
        }

        unitOfWork.UserRepository.Delete(user);

        if (await unitOfWork.CommitAsync(ct)) return Ok();

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-user-delete"));
    }

    /// <summary>
    /// Returns all users of this server
    /// </summary>
    /// <param name="includePending">This will include pending members</param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers(bool includePending = false)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.UserRepository.GetEmailConfirmedMemberDtosAsync(!includePending, ct));
    }

    /// <summary>
    /// Get Information about a given user
    /// </summary>
    /// <returns></returns>
    [HttpGet("profile-info")]
    [Authorize]
    [ProfilePrivacy]
    public async Task<ActionResult<MemberInfoDto>> GetProfileInfo(int userId)
    {
        var ct = HttpContext.RequestAborted;
        // Validate that the user has sharing enabled
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) return BadRequest();

        return Ok(mapper.Map<MemberInfoDto>(user));
    }

    /// <summary>
    /// Does the requested user have their profile sharing on
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("has-profile-shared")]
    [Authorize]
    public async Task<ActionResult<bool>> HasProfileShared(int userId)
    {
        var ct = HttpContext.RequestAborted;
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        return Ok(user?.UserPreferences?.SocialPreferences?.ShareProfile ?? false);
    }

    /// <summary>
    /// Is there any reading progress on this library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("has-reading-progress")]
    public async Task<ActionResult<bool>> HasReadingProgress(int libraryId)
    {
        var ct = HttpContext.RequestAborted;
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, ct: ct);
        if (library == null) return BadRequest(await localizationService.TranslateAsync(UserId, "library-doesnt-exist"));
        return Ok(await unitOfWork.AppUserProgressRepository.UserHasProgress(library.Type, UserId, ct));
    }

    /// <summary>
    /// Does the user have access to this library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("has-library-access")]
    public async Task< ActionResult<bool>> HasLibraryAccess(int libraryId)
    {
        var ct = HttpContext.RequestAborted;
        var libs = await unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(Username!, ct);
        return Ok(libs.Any(x => x.Id == libraryId));
    }

    /// <summary>
    /// Update the user preferences
    /// </summary>
    /// <remarks>If the user has ReadOnly role, they will not be able to perform this action</remarks>
    /// <param name="preferencesDto"></param>
    /// <returns></returns>
    [HttpPost("update-preferences")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences(UserPreferencesDto preferencesDto)
    {
        var ct = HttpContext.RequestAborted;
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
            AppUserIncludes.UserPreferences, ct);
        if (user == null) return Unauthorized();

        var existingPreferences = user.UserPreferences;

        existingPreferences.GlobalPageLayoutMode = preferencesDto.GlobalPageLayoutMode;
        existingPreferences.BlurUnreadSummaries = preferencesDto.BlurUnreadSummaries;
        existingPreferences.PromptForDownloadSize = preferencesDto.PromptForDownloadSize;
        existingPreferences.NoTransitions = preferencesDto.NoTransitions;
        existingPreferences.CollapseSeriesRelationships = preferencesDto.CollapseSeriesRelationships;
        existingPreferences.ColorScapeEnabled = preferencesDto.ColorScapeEnabled;
        existingPreferences.BookReaderHighlightSlots = preferencesDto.BookReaderHighlightSlots;
        existingPreferences.DataSaver = preferencesDto.DataSaver;
        existingPreferences.PromptForRereadsAfter = Math.Max(preferencesDto.PromptForRereadsAfter, 0);
        existingPreferences.OnDeckProgressDays = preferencesDto.OnDeckProgressDays;
        existingPreferences.OnDeckUpdateDays = preferencesDto.OnDeckUpdateDays;
        existingPreferences.CustomKeyBinds = preferencesDto.CustomKeyBinds;

        var allLibs = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id, ct))
            .Select(l => l.Id).ToList();
        var allCollections = (await unitOfWork.CollectionTagRepository.GetCollectionsForUserAsync(user.Id, ct: ct))
            .Select(l => l.Id).ToList();
        var allReadingLists = (await unitOfWork.ReadingListRepository.GetReadingListsForUserAsync(user.Id, true, ct: ct))
            .Select(l => l.Id).ToList();

        preferencesDto.SocialPreferences.SocialLibraries = preferencesDto.SocialPreferences.SocialLibraries
            .Where(allLibs.Contains).ToList();
        existingPreferences.SocialPreferences = preferencesDto.SocialPreferences;

        existingPreferences.OpdsPreferences = preferencesDto.OpdsPreferences;

        #region Kobo Preferences

        if (preferencesDto.KoboSyncPreferences.HasManagedCollection && !allCollections.Contains(preferencesDto.KoboSyncPreferences.ManagedCollection))
        {
            return BadRequest();
        }

        var koboPreferences = existingPreferences.KoboSyncPreferences;

        koboPreferences.SyncItemLimit =  preferencesDto.KoboSyncPreferences.SyncItemLimit;
        koboPreferences.ManagedCollection = preferencesDto.KoboSyncPreferences.ManagedCollection;
        koboPreferences.ForwardToKoboStore = preferencesDto.KoboSyncPreferences.ForwardToKoboStore;

        koboPreferences.LibrarySyncEnabled = preferencesDto.KoboSyncPreferences.LibrarySyncEnabled;
        koboPreferences.SyncLibraries = preferencesDto.KoboSyncPreferences.SyncLibraries
            .Where(allLibs.Contains).ToList();

        koboPreferences.CollectionSyncEnabled = preferencesDto.KoboSyncPreferences.CollectionSyncEnabled;
        koboPreferences.SyncCollections = preferencesDto.KoboSyncPreferences.SyncCollections
            .Where(allCollections.Contains).ToList();

        koboPreferences.ReadingListSyncEnabled = preferencesDto.KoboSyncPreferences.ReadingListSyncEnabled;
        koboPreferences.SyncReadingLists = preferencesDto.KoboSyncPreferences.SyncReadingLists
            .Where(allReadingLists.Contains).ToList();

        #endregion



        if (preferencesDto.Theme != null && existingPreferences.Theme.Id != preferencesDto.Theme?.Id)
        {
            var theme = await unitOfWork.SiteThemeRepository.GetTheme(preferencesDto.Theme!.Id);
            existingPreferences.Theme = theme ?? await unitOfWork.SiteThemeRepository.GetDefaultTheme();
        }

        if (localizationService.GetLocales().Select(l => l.FileName).Contains(preferencesDto.Locale))
        {
            existingPreferences.Locale = preferencesDto.Locale;
        }

        unitOfWork.UserRepository.Update(existingPreferences);

        if (!await unitOfWork.CommitAsync(ct)) return BadRequest(await localizationService.TranslateAsync(UserId, "generic-user-pref"));

        await eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id, ct);
        return Ok(preferencesDto);
    }

    /// <summary>
    /// Returns the preferences of the user
    /// </summary>
    /// <returns></returns>
    [HttpGet("get-preferences")]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences()
    {
        var ct = HttpContext.RequestAborted; // TODO: Why isn't this a Project?
        return mapper.Map<UserPreferencesDto>(
            await unitOfWork.UserRepository.GetPreferencesAsync(Username!, ct));
    }

    /// <summary>
    /// Returns all sync points for the currently active user
    /// </summary>
    /// <returns></returns>
    [HttpGet("kobo-sync-points")]
    public async Task<List<KoboSyncPointDto>> GetKoboSyncPoints()
    {
        var syncPoints = await unitOfWork.KoboSyncPointRepository.GetSyncPoints(UserId, HttpContext.RequestAborted);
        return syncPoints;
    }

    /// <summary>
    /// Delete a kobo sync point. By id
    /// </summary>
    /// <param name="syncPointId"></param>
    /// <returns></returns>
    [HttpDelete("kobo-sync-point")]
    public async Task<ActionResult> DeleteKoboSyncPoint(int syncPointId)
    {
        await unitOfWork.KoboSyncPointRepository.DeleteSyncPoint(UserId, syncPointId, HttpContext.RequestAborted);
        return Ok();
    }

    /// <summary>
    /// Undo removal of books for the given sync point
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("kobo-sync-point/undo-remove-books")]
    public async Task<ActionResult> UndoRemoveBooks(UndoKoboRemoveBookDto dto)
    {
        var ct = HttpContext.RequestAborted;

        var syncPoint = await unitOfWork.KoboSyncPointRepository.GetSyncPoint(UserId, dto.SyncPointId, ct);
        if (syncPoint == null)
        {
            return NotFound();
        }

        syncPoint.RemovedChapterIds = syncPoint.RemovedChapterIds
            .Where(rc => !dto.UndoBookIds.Contains(rc.ChapterId))
            .ToList();

        await unitOfWork.CommitAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Returns a list of the user names within the system
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("names")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserNames()
    {
        var ct = HttpContext.RequestAborted;
        return Ok((await unitOfWork.UserRepository.GetAllUsersAsync(ct: ct)).Select(u => u.UserName));
    }

    /// <summary>
    /// Returns all users with tokens registered and their token information. Does not send the tokens.
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    /// <returns></returns>
    [KPlus]
    [HttpGet("tokens")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IEnumerable<UserTokenInfoDto>>> GetUserTokens()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await scrobblingService.GetUserTokenInfo(ct));
    }
}
