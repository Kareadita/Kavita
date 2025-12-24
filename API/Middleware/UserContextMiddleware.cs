using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Entities.Progress;
using API.Services;
using API.Services.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Middleware;
#nullable enable

/// <summary>
/// Middleware that resolves user identity from various authentication methods
/// (JWT, Auth Key, OIDC) and provides a unified IUserContext for downstream components.
/// Must run after UseAuthentication() and UseAuthorization().
/// </summary>
public class UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        UserContext userContext)
    {
        try
        {
            userContext.Clear();

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = TryGetUserIdFromClaim(context.User, ClaimTypes.NameIdentifier);

                var username = context.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

                var roles = context.User.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (userId.HasValue && username != null)
                {
                    var authType = TryParseAuthTypeClaim(context.User) ?? AuthenticationType.Unknown;

                    userContext.SetUserContext(userId.Value, username, authType, roles);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving user context");
            // Don't break the pipeline, let authorization handle it
        }

        await next(context);
    }

    private static AuthenticationType? TryParseAuthTypeClaim(ClaimsPrincipal user)
    {
        var authTypeClaim = user.FindFirst("AuthType")?.Value;
        return authTypeClaim != null && Enum.TryParse<AuthenticationType>(authTypeClaim, out var authType)
            ? authType
            : null;
    }

    private static int? TryGetUserIdFromClaim(ClaimsPrincipal claims, string claimType)
    {
        var claim = claims.FindFirst(claimType);
        if (claim != null && int.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}
