using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace API.Helpers;

public static class JwtHelper
{
    /// <summary>
    /// Extracts the expiration date from a JWT token.
    /// </summary>
    public static DateTime GetTokenExpiry(string jwtToken)
    {
        if (string.IsNullOrEmpty(jwtToken))
            return DateTime.MinValue;

        // Parse the JWT and extract the expiry claim
        var jwtHandler = new JwtSecurityTokenHandler();
        var token = jwtHandler.ReadJwtToken(jwtToken);
        var exp = token.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;

        if (long.TryParse(exp, out var expSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }

        return DateTime.MinValue;
    }

    /// <summary>
    /// Checks if a JWT token is valid based on its expiry date.
    /// </summary>
    public static bool IsTokenValid(string jwtToken)
    {
        if (string.IsNullOrEmpty(jwtToken)) return false;

        var expiry = GetTokenExpiry(jwtToken);
        return expiry > DateTime.UtcNow;
    }
}
