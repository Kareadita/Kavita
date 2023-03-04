using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class UsersController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IEventHub _eventHub;

    public UsersController(IUnitOfWork unitOfWork, IMapper mapper, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _eventHub = eventHub;
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("delete-user")]
    public async Task<ActionResult> DeleteUser(string username)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        _unitOfWork.UserRepository.Delete(user);

        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest("Could not delete the user.");
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers()
    {
        return Ok(await _unitOfWork.UserRepository.GetEmailConfirmedMemberDtosAsync());
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetPendingUsers()
    {
        return Ok(await _unitOfWork.UserRepository.GetPendingMemberDtosAsync());
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
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        return Ok(await _unitOfWork.AppUserProgressRepository.UserHasProgress(library.Type, userId));
    }

    [HttpGet("has-library-access")]
    public async Task<ActionResult<bool>> HasLibraryAccess(int libraryId)
    {
        var libs = await _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(User.GetUsername());
        return Ok(libs.Any(x => x.Id == libraryId));
    }

    [HttpPost("update-preferences")]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences(UserPreferencesDto preferencesDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
            AppUserIncludes.UserPreferences);
        var existingPreferences = user.UserPreferences;

        preferencesDto.Theme ??= await _unitOfWork.SiteThemeRepository.GetDefaultTheme();

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
        existingPreferences.Theme = await _unitOfWork.SiteThemeRepository.GetThemeById(preferencesDto.Theme.Id);
        existingPreferences.LayoutMode = preferencesDto.LayoutMode;
        existingPreferences.PromptForDownloadSize = preferencesDto.PromptForDownloadSize;
        existingPreferences.NoTransitions = preferencesDto.NoTransitions;
        existingPreferences.SwipeToPaginate = preferencesDto.SwipeToPaginate;

        _unitOfWork.UserRepository.Update(existingPreferences);

        if (await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName), user.Id);
            return Ok(preferencesDto);
        }

        return BadRequest("There was an issue saving preferences.");
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
}
