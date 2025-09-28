using System;
using System.Net;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

/// <summary>
/// Middleware that checks if Opds has been enabled for this server, and sets OpdsController.UserId in HttpContext
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OpdsActionFilterAttribute(IUnitOfWork unitOfWork, ILocalizationService localizationService, ILogger<OpdsController> logger): ActionFilterAttribute
{

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        int userId;
        try
        {
            if (!context.ActionArguments.TryGetValue("apiKey", out var apiKeyObj) || apiKeyObj is not string apiKey)
            {
                context.Result = new BadRequestResult();
                return;
            }

            userId = await unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
            if (userId == null || userId == 0)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            if (!settings.EnableOpds)
            {
                context.Result = new ContentResult
                {
                    Content = await localizationService.Translate(userId, "opds-disabled"),
                    ContentType = "text/plain",
                    StatusCode = (int)HttpStatusCode.BadRequest,
                };
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to handle OPDS request");
            context.Result = new BadRequestResult();
            return;
        }

        // Add the UserId from ApiKey onto the OPDSController
        context.HttpContext.Items.Add(OpdsController.UserId, userId);


        await next();
    }

}
