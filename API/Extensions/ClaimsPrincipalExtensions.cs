using System.Security.Claims;
using Kavita.Common;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUsername(this ClaimsPrincipal user)
    {
        var userClaim = user.FindFirst(JwtRegisteredClaimNames.Name);
        if (userClaim == null) throw new KavitaException("User is not authenticated");
        return userClaim.Value;
    }

    public static int GetUserId(this ClaimsPrincipal user)
    {
        var userClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userClaim == null) throw new KavitaException("User is not authenticated");
        return int.Parse(userClaim.Value);
    }
}
