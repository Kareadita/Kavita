using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Kavita.Common;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace API.Extensions;
#nullable enable

public static class ClaimsPrincipalExtensions
{
    private const string NotAuthenticatedMessage = "User is not authenticated";
    private const string EmailVerifiedClaimType = "email_verified";

    /// <summary>
    /// Gets the authenticated user's username
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

    public static bool HasVerifiedEmail(this ClaimsPrincipal user)
    {
        var emailVerified = user.FindFirst(EmailVerifiedClaimType);
        if (emailVerified == null) return false;

        if (!bool.TryParse(emailVerified.Value, out bool emailVerifiedValue) || !emailVerifiedValue)
        {
            return false;
        }

        return true;
    }

    public static IList<string> GetClaimsWithPrefix(this ClaimsPrincipal claimsPrincipal, string claimType, string prefix)
    {
        return claimsPrincipal
            .FindAll(claimType)
            .Where(c => c.Value.StartsWith(prefix))
            .Select(c => c.Value.TrimPrefix(prefix))
            .ToList();
    }
}
