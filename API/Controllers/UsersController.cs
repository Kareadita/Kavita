using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.KavitaPlus.Account;
using API.Middleware;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

[Authorize]
public class UsersController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IEventHub _eventHub;
    private readonly ILocalizationService _localizationService;
    private readonly ILicenseService _licenseService;

    public UsersController(IUnitOfWork unitOfWork, IMapper mapper, IEventHub eventHub,
        ILocalizationService localizationService, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _eventHub = eventHub;
        _localizationService = localizationService;
        _licenseService = licenseService;
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpDelete("delete-user")]
    public async Task<ActionResult> DeleteUser(string username)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        if (user == null) return BadRequest();

        // Remove all likes for the user, so like counts are correct
        var annotations = await _unitOfWork.AnnotationRepository.GetAllAnnotations();
        foreach (var annotation in annotations.Where(a => a.Likes.Contains(user.Id)))
        {
            annotation.Likes.Remove(user.Id);
            _unitOfWork.AnnotationRepository.Update(annotation);
        }

        _unitOfWork.UserRepository.Delete(user);

        //(TODO: After updating a role or removing a user, delete their token)
        // await _userManager.RemoveAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);

        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest(await _localizationService.Translate(UserId, "generic-user-delete"));
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
        return Ok(await _unitOfWork.UserRepository.GetEmailConfirmedMemberDtosAsync(!includePending));
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
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return BadRequest();

        return Ok(_mapper.Map<MemberInfoDto>(user));
    }


    [HttpGet("has-reading-progress")]
    public async Task<ActionResult<bool>> HasReadingProgress(int libraryId)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        if (library == null) return BadRequest(await _localizationService.Translate(UserId, "library-doesnt-exist"));
        return Ok(await _unitOfWork.AppUserProgressRepository.UserHasProgress(library.Type, UserId));
    }

    [HttpGet("has-library-access")]
    public async Task< ActionResult<bool>> HasLibraryAccess(int libraryId)
    {
        var libs = await _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(Username!);
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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
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

        var allLibs = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id))
            .Select(l => l.Id).ToList();

        preferencesDto.SocialPreferences.SocialLibraries = preferencesDto.SocialPreferences.SocialLibraries
            .Where(l => allLibs.Contains(l)).ToList();
        existingPreferences.SocialPreferences = preferencesDto.SocialPreferences;

        existingPreferences.OpdsPreferences = preferencesDto.OpdsPreferences;

        if (await _licenseService.HasActiveLicense())
        {
            existingPreferences.AniListScrobblingEnabled = preferencesDto.AniListScrobblingEnabled;
            existingPreferences.WantToReadSync = preferencesDto.WantToReadSync;
        }



        if (preferencesDto.Theme != null && existingPreferences.Theme.Id != preferencesDto.Theme?.Id)
        {
            var theme = await _unitOfWork.SiteThemeRepository.GetTheme(preferencesDto.Theme!.Id);
            existingPreferences.Theme = theme ?? await _unitOfWork.SiteThemeRepository.GetDefaultTheme();
        }


        if (_localizationService.GetLocales().Select(l => l.FileName).Contains(preferencesDto.Locale))
        {
            existingPreferences.Locale = preferencesDto.Locale;
        }


        _unitOfWork.UserRepository.Update(existingPreferences);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(UserId, "generic-user-pref"));

        await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id);
        return Ok(preferencesDto);
    }

    /// <summary>
    /// Returns the preferences of the user
    /// </summary>
    /// <returns></returns>
    [HttpGet("get-preferences")]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences()
    {
        return _mapper.Map<UserPreferencesDto>(
            await _unitOfWork.UserRepository.GetPreferencesAsync(Username!));

    }

    /// <summary>
    /// Returns a list of the user names within the system
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("names")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserNames()
    {
        return Ok((await _unitOfWork.UserRepository.GetAllUsersAsync()).Select(u => u.UserName));
    }

    /// <summary>
    /// Returns all users with tokens registered and their token information. Does not send the tokens.
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("tokens")]
    public async Task<ActionResult<IEnumerable<UserTokenInfo>>> GetUserTokens()
    {
        if (!await _licenseService.HasActiveLicense()) return BadRequest(_localizationService.Translate(UserId, "kavitaplus-restricted"));

        return Ok((await _unitOfWork.UserRepository.GetUserTokenInfo()));
    }
}
