using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Models.Constants;
using Kavita.Models.Entities.Progress;
using Kavita.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
        var unitOfWork = ctx.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEventsHelper>>();

        if (!int.TryParse(ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            ctx.Fail("No valid user id");
            return;
        }

        // Hot path, smart select & check
        var securityStamp = await unitOfWork.DataContext.AppUser
            .Where(u => u.Id == userId)
            .Where(u => u.UserRoles.Any(r => r.Role.Name == PolicyConstants.AdminRole)
            || u.UserRoles.Any(r => r.Role.Name == PolicyConstants.LoginRole))
            .Select(u => u.SecurityStamp)
            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);
        if (securityStamp is null)
        {
            ctx.Fail("User does not exist or is not allowed to log in");
            return;
        }

        var stampClaim = ctx.Principal?.FindFirst(TokenService.SecurityStampClaimType)?.Value;
        if (stampClaim != securityStamp)
        {
            logger.LogDebug("[JwtBearer] Rejecting token: security stamp mismatch for user {UserId}", userId);
            ctx.Fail("Security stamp has changed");
            return;
        }

        (ctx.Principal?.Identity as ClaimsIdentity)?.AddClaim(new Claim("AuthType", nameof(AuthenticationType.JWT)));
    }
}
