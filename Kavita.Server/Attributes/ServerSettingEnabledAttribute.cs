using System;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Store;
using Kavita.Models.Entities.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Server.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ServerSettingEnabledAttribute(ServerSettingKey key, string locKey, bool allowUnauthenticated = false) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!allowUnauthenticated && !userContext.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(key, context.HttpContext.RequestAborted);
        if (!bool.TryParse(setting.Value, out var result) || !result)
        {
            var localizationService = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            var message = await localizationService.TranslateAsync(locKey);

            context.Result = new ObjectResult(new {Message = message})
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
