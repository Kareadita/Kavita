using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.API.Errors;
using Kavita.Common;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Identity;

namespace Kavita.API.Services;

public interface IAccountService
{
    Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword);
    Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password);
    Task<IEnumerable<ApiException>> ValidateUsername(string? username);
    Task<IEnumerable<ApiException>> ValidateEmail(string email);
    Task<bool> HasBookmarkPermission(AppUser? user);
    Task<bool> HasDownloadPermission(AppUser? user);
    Task<bool> CanChangeAgeRestriction(AppUser? user);

    /// <summary>
    ///
    /// </summary>
    /// <param name="actingUserId">The user who is changing the identity</param>
    /// <param name="user">the user being changed</param>
    /// <param name="identityProvider"> the provider being changed to</param>
    /// <returns>If true, user should not be updated by kavita (anymore)</returns>
    /// <exception cref="KavitaException">Throws if invalid actions are being performed</exception>
    Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider);
    /// <summary>
    /// Removes access to all libraries, then grant access to all given libraries or all libraries if the user is admin.
    /// Creates side nav streams as well
    /// </summary>
    /// <param name="user"></param>
    /// <param name="librariesIds"></param>
    /// <param name="hasAdminRole"></param>
    /// <returns></returns>
    /// <remarks>Ensure that the users SideNavStreams are loaded</remarks>
    /// <remarks>Does NOT commit</remarks>
    Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole);
    Task<IEnumerable<IdentityError>> UpdateRolesForUser(AppUser user, IList<string> roles);
    /// <summary>
    /// Seeds all information necessary for a new user
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    Task SeedUser(AppUser user);
    void AddDefaultStreamsToUser(AppUser user);
    Task AddDefaultReadingProfileToUser(AppUser user);
}
