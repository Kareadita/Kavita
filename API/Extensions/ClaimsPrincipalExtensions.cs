using System.Security.Claims;
using Kavita.Common;

namespace API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUsername(this ClaimsPrincipal user)
    {
        var userClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userClaim == null) throw new KavitaException("User is not authenticated");
        return userClaim.Value;
    }
}
