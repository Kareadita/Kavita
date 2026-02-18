using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Errors;
using Kavita.Common;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Identity;

namespace Kavita.API.Services;

public interface IAccountService
{
    Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword, CancellationToken ct = default);
    Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password, CancellationToken ct = default);
    Task<IEnumerable<ApiException>> ValidateUsername(string? username, CancellationToken ct = default);
    Task<IEnumerable<ApiException>> ValidateEmail(string email, CancellationToken ct = default);
    Task<bool> CanChangeAgeRestriction(AppUser? user, CancellationToken ct = default);

    /// <summary>
    ///
    /// </summary>
    /// <param name="actingUserId">The user who is changing the identity</param>
    /// <param name="user">the user being changed</param>
    /// <param name="identityProvider"> the provider being changed to</param>
    /// <param name="ct"></param>
    /// <returns>If true, user should not be updated by kavita (anymore)</returns>
    /// <exception cref="KavitaException">Throws if invalid actions are being performed</exception>
    Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider, CancellationToken ct = default);

    /// <summary>
    /// Removes access to all libraries, then grant access to all given libraries or all libraries if the user is admin.
    /// Creates side nav streams as well
    /// </summary>
    /// <param name="user"></param>
    /// <param name="librariesIds"></param>
    /// <param name="hasAdminRole"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>Ensure that the users SideNavStreams are loaded</remarks>
    /// <remarks>Does NOT commit</remarks>
    Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole, CancellationToken ct = default);
    Task<IEnumerable<IdentityError>> UpdateRolesForUser(AppUser user, IList<string> roles, CancellationToken ct = default);

    /// <summary>
    /// Seeds all information necessary for a new user
    /// </summary>
    /// <param name="user"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SeedUser(AppUser user, CancellationToken ct = default);
    void AddDefaultStreamsToUser(AppUser user, CancellationToken ct = default);
    Task AddDefaultReadingProfileToUser(AppUser user, CancellationToken ct = default);
}
