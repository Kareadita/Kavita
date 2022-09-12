using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities;
using API.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IAccountService
{
    Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword);
    Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password);
    Task<IEnumerable<ApiException>> ValidateUsername(string username);
    Task<IEnumerable<ApiException>> ValidateEmail(string email);
    Task<bool> HasBookmarkPermission(AppUser user);
    Task<bool> HasDownloadPermission(AppUser appuser);
}

public class AccountService : IAccountService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<AccountService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    public const string DefaultPassword = "[k.2@RZ!mxCQkJzE";

    public AccountService(UserManager<AppUser> userManager, ILogger<AccountService> logger, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword)
    {
        var passwordValidationIssues = (await ValidatePassword(user, newPassword)).ToList();
        if (passwordValidationIssues.Any()) return passwordValidationIssues;

        var result = await _userManager.RemovePasswordAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Could not update password");
            return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
        }


        result = await _userManager.AddPasswordAsync(user, newPassword);
        if (!result.Succeeded)
        {
            _logger.LogError("Could not update password");
            return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
        }

        return new List<ApiException>();
    }

    public async Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password)
    {
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, user, password);
            if (!validationResult.Succeeded)
            {
                return validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description));
            }
        }

        return Array.Empty<ApiException>();
    }
    public async Task<IEnumerable<ApiException>> ValidateUsername(string username)
    {
        if (await _userManager.Users.AnyAsync(x => x.NormalizedUserName == username.ToUpper()))
        {
            return new List<ApiException>()
            {
                new ApiException(400, "Username is already taken")
            };
        }

        return Array.Empty<ApiException>();
    }

    public async Task<IEnumerable<ApiException>> ValidateEmail(string email)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(email);
        if (user == null) return Array.Empty<ApiException>();

        return new List<ApiException>()
        {
            new ApiException(400, "Email is already registered")
        };
    }

    /// <summary>
    /// Does the user have the Bookmark permission or admin rights
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> HasBookmarkPermission(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(PolicyConstants.BookmarkRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    /// <summary>
    /// Does the user have the Download permission or admin rights
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> HasDownloadPermission(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(PolicyConstants.DownloadRole) || roles.Contains(PolicyConstants.AdminRole);
    }

}
