using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Kavita.API.Services;

public interface IOidcService
{
    /// <summary>
    /// Returns the user authenticated with OpenID Connect
    /// </summary>
    /// <param name="request"></param>
    /// <param name="principal"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">if any requirements aren't met</exception>
    Task<AppUser?> LoginOrCreate(HttpRequest request, ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>
    /// Refresh the token inside the cookie when it's close to expiring. And sync the user
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>If the token is refreshed successfully, updates the last active time of the suer</remarks>
    Task<AppUser?> RefreshCookieToken(CookieValidatePrincipalContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Remove <see cref="AppUser.OidcId"/> from all users
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearOidcIds(CancellationToken ct = default);
}
