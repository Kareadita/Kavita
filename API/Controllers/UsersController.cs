using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.KavitaPlus.Account;
using API.Extensions;
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

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("delete-user")]
    public async Task<ActionResult> DeleteUser(string username)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        _unitOfWork.UserRepository.Delete(user);

        //(TODO: After updating a role or removing a user, delete their token)
        // await _userManager.RemoveAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);

        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-user-delete"));
    }

    /// <summary>
    /// Returns all users of this server
    /// </summary>
    /// <param name="includePending">This will include pending members</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers(bool includePending = false)
    {
        return Ok(await _unitOfWork.UserRepository.GetEmailConfirmedMemberDtosAsync(!includePending));
    }

    [HttpGet("myself")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetMyself()
    {
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync();
        return Ok(users.Where(u => u.UserName == User.GetUsername()).DefaultIfEmpty().Select(u => _mapper.Map<MemberDto>(u)).SingleOrDefault());
    }


    [HttpGet("has-reading-progress")]
    public async Task<ActionResult<bool>> HasReadingProgress(int libraryId)
    {
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        if (library == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "library-doesnt-exist"));
        return Ok(await _unitOfWork.AppUserProgressRepository.UserHasProgress(library.Type, User.GetUserId()));
    }

    [HttpGet("has-library-access")]
    public ActionResult<bool> HasLibraryAccess(int libraryId)
    {
        var libs = _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(User.GetUsername());
        return Ok(libs.Any(x => x.Id == libraryId));
    }

    /// <summary>
    /// Update the user preferences
    /// </summary>
    /// <remarks>If the user has ReadOnly role, they will not be able to perform this action</remarks>
    /// <param name="preferencesDto"></param>
    /// <returns></returns>
    [HttpPost("update-preferences")]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences(UserPreferencesDto preferencesDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
            AppUserIncludes.UserPreferences);
        if (user == null) return Unauthorized();
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var existingPreferences = user!.UserPreferences;

        existingPreferences.ReadingDirection = preferencesDto.ReadingDirection;
        existingPreferences.ScalingOption = preferencesDto.ScalingOption;
        existingPreferences.PageSplitOption = preferencesDto.PageSplitOption;
        existingPreferences.AutoCloseMenu = preferencesDto.AutoCloseMenu;
        existingPreferences.ShowScreenHints = preferencesDto.ShowScreenHints;
        existingPreferences.EmulateBook = preferencesDto.EmulateBook;
        existingPreferences.ReaderMode = preferencesDto.ReaderMode;
        existingPreferences.LayoutMode = preferencesDto.LayoutMode;
        existingPreferences.BackgroundColor = string.IsNullOrEmpty(preferencesDto.BackgroundColor) ? "#000000" : preferencesDto.BackgroundColor;
        existingPreferences.BookReaderMargin = preferencesDto.BookReaderMargin;
        existingPreferences.BookReaderLineSpacing = preferencesDto.BookReaderLineSpacing;
        existingPreferences.BookReaderFontFamily = preferencesDto.BookReaderFontFamily;
        existingPreferences.BookReaderFontSize = preferencesDto.BookReaderFontSize;
        existingPreferences.BookReaderTapToPaginate = preferencesDto.BookReaderTapToPaginate;
        existingPreferences.BookReaderReadingDirection = preferencesDto.BookReaderReadingDirection;
        existingPreferences.BookReaderWritingStyle = preferencesDto.BookReaderWritingStyle;
        existingPreferences.BookThemeName = preferencesDto.BookReaderThemeName;
        existingPreferences.BookReaderLayoutMode = preferencesDto.BookReaderLayoutMode;
        existingPreferences.BookReaderImmersiveMode = preferencesDto.BookReaderImmersiveMode;
        existingPreferences.GlobalPageLayoutMode = preferencesDto.GlobalPageLayoutMode;
        existingPreferences.BlurUnreadSummaries = preferencesDto.BlurUnreadSummaries;
        existingPreferences.LayoutMode = preferencesDto.LayoutMode;
        existingPreferences.PromptForDownloadSize = preferencesDto.PromptForDownloadSize;
        existingPreferences.NoTransitions = preferencesDto.NoTransitions;
        existingPreferences.SwipeToPaginate = preferencesDto.SwipeToPaginate;
        existingPreferences.CollapseSeriesRelationships = preferencesDto.CollapseSeriesRelationships;
        existingPreferences.ShareReviews = preferencesDto.ShareReviews;

        existingPreferences.PdfTheme = preferencesDto.PdfTheme;
        existingPreferences.PdfScrollMode = preferencesDto.PdfScrollMode;
        existingPreferences.PdfSpreadMode = preferencesDto.PdfSpreadMode;

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


        if (_localizationService.GetLocales().Contains(preferencesDto.Locale))
        {
            existingPreferences.Locale = preferencesDto.Locale;
        }


        _unitOfWork.UserRepository.Update(existingPreferences);

        if (!await _unitOfWork.CommitAsync()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-user-pref"));

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
            await _unitOfWork.UserRepository.GetPreferencesAsync(User.GetUsername()));

    }

    /// <summary>
    /// Returns a list of the user names within the system
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
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
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("tokens")]
    public async Task<ActionResult<IEnumerable<UserTokenInfo>>> GetUserTokens()
    {
        if (!await _licenseService.HasActiveLicense()) return BadRequest(_localizationService.Translate(User.GetUserId(), "kavitaplus-restricted"));

        return Ok((await _unitOfWork.UserRepository.GetUserTokenInfo()));
    }
}
