using System;
using System.IdentityModel.Tokens.Jwt;

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

        return token.ValidTo;
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
