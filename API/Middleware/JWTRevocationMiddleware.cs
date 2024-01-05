using System.Threading.Tasks;
using API.Constants;
using EasyCaching.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

/// <summary>
/// Responsible for maintaining an in-memory. Not in use
/// </summary>
public class JwtRevocationMiddleware(
    RequestDelegate next,
    IEasyCachingProviderFactory cacheFactory,
    ILogger<JwtRevocationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity is {IsAuthenticated: false})
        {
            await next(context);
            return;
        }

        // Get the JWT from the request headers or wherever you store it
        var token = context.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", string.Empty);

        // Check if the token is revoked
        if (await IsTokenRevoked(token))
        {
            logger.LogWarning("Revoked token detected: {Token}", token);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private async Task<bool> IsTokenRevoked(string token)
    {
        // Check if the token exists in the revocation list stored in the cache
        var isRevoked = await cacheFactory.GetCachingProvider(EasyCacheProfiles.RevokedJwt)
            .GetAsync<string>(token);


        return isRevoked.HasValue;
    }
}
