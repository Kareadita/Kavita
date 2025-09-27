using System;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.SignalR.Presence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

/// <summary>
/// Middleware that will track any API calls as updating the authenticated (ApiKey) user's LastActive and inform <see cref="PresenceTracker"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OpdsActiveUserMiddlewareAttribute(IUnitOfWork unitOfWork, IPresenceTracker presenceTracker, ILogger<OpdsController> logger) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            if (!context.ActionArguments.TryGetValue("apiKey", out var apiKeyObj) || apiKeyObj is not string apiKey)
            {
                await next();
                return;
            }

            var userId = await unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
            if (userId == 0)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await unitOfWork.UserRepository.UpdateUserAsActive(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to count User as active during OPDS request");
            await next();
            return;
        }

        await next();
    }
}
