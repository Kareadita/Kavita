using System;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Server.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class KPlusAttribute(bool allowUnauthenticated = false) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!allowUnauthenticated && !userContext.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var licenseService = context.HttpContext.RequestServices.GetRequiredService<ILicenseService>();

        if (!await licenseService.HasActiveLicense(ct: context.HttpContext.RequestAborted))
        {
            var localizationService = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            var message = await localizationService.TranslateAsync("kavitaplus-restricted");

            context.Result = new BadRequestObjectResult(new {Message = message});
        }

    }
}
