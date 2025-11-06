using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Progress;
using API.Services;
using API.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace API.Middleware;
#nullable enable

/// <summary>
/// Middleware that resolves user identity from various authentication methods
/// (JWT, API Key, OIDC) and provides a unified IUserContext for downstream components.
/// Must run after UseAuthentication() and UseAuthorization().
/// </summary>
public class UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> logger, HybridCache cache)
{
    private static readonly HybridCacheEntryOptions ApiKeyCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(15)
    };


    public async Task InvokeAsync(
        HttpContext context,
        UserContext userContext,  // Scoped service
        IUnitOfWork unitOfWork)
    {
        try
        {
            // Clear any previous context (shouldn't be necessary, but defensive)
            userContext.Clear();

            // Check if endpoint allows anonymous access
            var endpoint = context.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

            // ALWAYS attempt to resolve user identity, regardless of [AllowAnonymous]
            var (userId, username, authType) = await ResolveUserIdentityAsync(context, unitOfWork);

            if (userId.HasValue)
            {
                userContext.SetUserContext(userId.Value, username!, authType);

                logger.LogTrace(
                    "Resolved user context: UserId={UserId}, AuthType={AuthType}",
                    userId, authType);
            }
            else if (!allowAnonymous)
            {
                // No user resolved on a protected endpoint - this is a problem
                // Authorization middleware will handle returning 401/403
                logger.LogWarning("Could not resolve user identity for protected endpoint: {Path}", context.Request.Path);
            }
            else
            {
                // No user resolved but endpoint allows anonymous - this is fine
                logger.LogTrace("No user identity resolved for anonymous endpoint: {Path}", context.Request.Path);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving user context");
            // Don't break the pipeline, let authorization handle it
        }

        await next(context);
    }

    private async Task<(int? userId, string? username, AuthenticationType authType)> ResolveUserIdentityAsync(
        HttpContext context,
        IUnitOfWork unitOfWork)
    {
        // Priority 1: ALWAYS check for API Key first (query string or path parameter)
        // API keys work even on [AllowAnonymous] endpoints (like OPDS)
        var apiKeyResult = await TryResolveFromApiKeyAsync(context, unitOfWork);
        if (apiKeyResult.userId.HasValue)
        {
            return apiKeyResult;
        }

        // Priority 2: Check for JWT or OIDC claims
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return ResolveFromClaims(context);
        }

        return (null, null, AuthenticationType.Unknown);
    }

    private async Task<(int? userId, string? username, AuthenticationType authType)> TryResolveFromApiKeyAsync(
        HttpContext context,
        IUnitOfWork unitOfWork)
    {
        string? apiKey = null;

        // Check query string: ?apiKey=xxx
        if (context.Request.Query.TryGetValue("apiKey", out var apiKeyQuery))
        {
            apiKey = apiKeyQuery.ToString();
        }

        // Check path for OPDS endpoints: /api/opds/{apiKey}/...
        if (string.IsNullOrEmpty(apiKey))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Contains("/api/opds/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var opdsIndex = Array.FindIndex(segments, s =>
                    s.Equals("opds", StringComparison.OrdinalIgnoreCase));

                if (opdsIndex >= 0 && opdsIndex + 1 < segments.Length)
                {
                    apiKey = segments[opdsIndex + 1];
                }
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return (null, null, AuthenticationType.Unknown);
        }

        try
        {
            var cacheKey = $"apikey_{apiKey}";

            var result = await cache.GetOrCreateAsync(
                cacheKey,
                (apiKey, unitOfWork),
                async (state, cancel) =>
                {
                    var user = await state.unitOfWork.UserRepository.GetUserDtoByApiKeyAsync(state.apiKey);
                    return (user?.Id, user?.Username);
                },
                ApiKeyCacheOptions,
                cancellationToken: context.RequestAborted);

            if (result is {Id: not null, Username: not null})
            {
                logger.LogDebug("Resolved user {UserId} from API key for path {Path}", result.Id, context.Request.Path);

                return (result.Id, result.Username, AuthenticationType.ApiKey);
            }

            logger.LogWarning("Invalid API key provided for path {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve user from API key");
        }

        return (null, null, AuthenticationType.Unknown);
    }

    private static (int? userId, string? username, AuthenticationType authType) ResolveFromClaims(HttpContext context)
    {
        var claims = context.User;

        // Check if OIDC authentication
        if (context.Request.Cookies.ContainsKey(OidcService.CookieName))
        {
            var userId = TryGetUserIdFromClaim(claims, ClaimTypes.NameIdentifier);
            var username = claims.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

            return (userId, username, AuthenticationType.OIDC);
        }

        // JWT authentication
        var jwtUserId = TryGetUserIdFromClaim(claims, ClaimTypes.NameIdentifier);
        var jwtUsername = claims.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

        return (jwtUserId, jwtUsername, AuthenticationType.JWT);
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
