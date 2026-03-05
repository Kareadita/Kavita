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
    ILicenseService licenseService)
    : BaseApiController
{
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpDelete("delete-user")]
    public async Task<ActionResult> DeleteUser(string username)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        if (user == null) return BadRequest();

        // Remove all likes for the user, so like counts are correct
        var annotations = await unitOfWork.AnnotationRepository.GetAllAnnotations();
        foreach (var annotation in annotations.Where(a => a.Likes.Contains(user.Id)))
        {
            annotation.Likes.Remove(user.Id);
            unitOfWork.AnnotationRepository.Update(annotation);
        }

        unitOfWork.UserRepository.Delete(user);

        if (await unitOfWork.CommitAsync()) return Ok();

        return BadRequest(await localizationService.Translate(UserId, "generic-user-delete"));
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
        return Ok(await unitOfWork.UserRepository.GetEmailConfirmedMemberDtosAsync(!includePending));
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
        // Validate that the user has sharing enabled
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
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
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
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
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        if (library == null) return BadRequest(await localizationService.Translate(UserId, "library-doesnt-exist"));
        return Ok(await unitOfWork.AppUserProgressRepository.UserHasProgress(library.Type, UserId));
    }

    /// <summary>
    /// Does the user have access to this library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("has-library-access")]
    public async Task< ActionResult<bool>> HasLibraryAccess(int libraryId)
    {
        var libs = await unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(Username!);
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
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
            AppUserIncludes.UserPreferences);
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
        existingPreferences.CustomKeyBinds = preferencesDto.CustomKeyBinds;

        var allLibs = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id))
            .Select(l => l.Id).ToList();

        preferencesDto.SocialPreferences.SocialLibraries = preferencesDto.SocialPreferences.SocialLibraries
            .Where(l => allLibs.Contains(l)).ToList();
        existingPreferences.SocialPreferences = preferencesDto.SocialPreferences;

        existingPreferences.OpdsPreferences = preferencesDto.OpdsPreferences;

        if (await licenseService.HasActiveLicense(ct: HttpContext.RequestAborted))
        {
            existingPreferences.AniListScrobblingEnabled = preferencesDto.AniListScrobblingEnabled;
            existingPreferences.WantToReadSync = preferencesDto.WantToReadSync;
        }



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

        if (!await unitOfWork.CommitAsync()) return BadRequest(await localizationService.Translate(UserId, "generic-user-pref"));

        await eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id);
        return Ok(preferencesDto);
    }

    /// <summary>
    /// Returns the preferences of the user
    /// </summary>
    /// <returns></returns>
    [HttpGet("get-preferences")]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences()
    {
        return mapper.Map<UserPreferencesDto>(
            await unitOfWork.UserRepository.GetPreferencesAsync(Username!));

    }

    /// <summary>
    /// Returns a list of the user names within the system
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("names")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserNames()
    {
        return Ok((await unitOfWork.UserRepository.GetAllUsersAsync()).Select(u => u.UserName));
    }

    /// <summary>
    /// Returns all users with tokens registered and their token information. Does not send the tokens.
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    /// <returns></returns>
    [KPlus]
    [HttpGet("tokens")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IEnumerable<UserTokenInfo>>> GetUserTokens()
    {
        return Ok(await unitOfWork.UserRepository.GetUserTokenInfo());
    }
}
