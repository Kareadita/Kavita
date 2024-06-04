using System.Security.Claims;
using Kavita.Common;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace API.Extensions;
#nullable enable

public static class ClaimsPrincipalExtensions
{
    private const string NotAuthenticatedMessage = "User is not authenticated";
    /// <summary>
    /// Get's the authenticated user's username
    /// </summary>
    /// <remarks>Warning! Username's can contain .. and /, do not use folders or filenames explicitly with the Username</remarks>
    /// <param name="user"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public static string GetUsername(this ClaimsPrincipal user)
    {
        var userClaim = user.FindFirst(JwtRegisteredClaimNames.Name) ?? throw new KavitaException(NotAuthenticatedMessage);
        return userClaim.Value;
    }

    public static int GetUserId(this ClaimsPrincipal user)
    {
        var userClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? throw new KavitaException(NotAuthenticatedMessage);
        return int.Parse(userClaim.Value);
    }
}
