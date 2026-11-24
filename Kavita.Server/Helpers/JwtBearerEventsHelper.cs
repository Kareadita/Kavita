using System.Security.Claims;
using System.Threading.Tasks;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services;
using Kavita.Services.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Helpers;

public class JwtBearerEventsHelper : JwtBearerEvents
{
    public JwtBearerEventsHelper()
    {
        OnMessageReceived = SetTokenFromQuery;
        OnTokenValidated = HandleTokenValidated;
    }

    private static Task SetTokenFromQuery(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        // Only use query string based token on SignalR hubs
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }

    private static async Task HandleTokenValidated(TokenValidatedContext ctx)
    {
        var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEventsHelper>>();

        var userId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            logger.LogDebug("[JwtBearer] Rejecting token: user {UserId} no longer exists", userId);
            ctx.Fail("User no longer exists");
            return;
        }

        if (!await userManager.IsEnabledAsync(user))
        {
            logger.LogDebug("[JwtBearer] Rejecting token: user {UserId} is not allowed to login", userId);
            ctx.Fail("Account is disabled");
            return;
        }

        // Don't enforce check on old tokens
        var stampClaim = ctx.Principal?.FindFirst(TokenService.SecurityStampClaimType)?.Value;
        if (!string.IsNullOrEmpty(stampClaim) && stampClaim != user.SecurityStamp)
        {
            logger.LogDebug("[JwtBearer] Rejecting token: security stamp mismatch for user {UserId}", userId);
            ctx.Fail("Security stamp has changed");
            return;
        }

        (ctx.Principal?.Identity as ClaimsIdentity)?.AddClaim(new Claim("AuthType", nameof(AuthenticationType.JWT)));
    }
}
