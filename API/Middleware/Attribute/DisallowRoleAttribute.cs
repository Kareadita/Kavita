using System;
using System.Linq;
using System.Threading.Tasks;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace API.Middleware;

/// <summary>
/// An attribute to prevent users with certain roles to access resources, or do actions.
/// Returns 400 BadRequest to prevent logouts in the UI. If you want an 401Unauthorized response,
/// use the Authorize attribute and require roles instead
/// </summary>
/// <param name="roles">Roles which should not be allowed to access the annotated resource</param>
/// <remarks>This attribute should be used together with Authorize</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class DisallowRoleAttribute(params string[] roles) : Attribute, IAsyncAuthorizationFilter
{

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (roles.Any(role => !string.IsNullOrEmpty(role) && user.IsInRole(role)))
        {
            var localizationService = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            var userId = user.GetUserId();

            var message = await localizationService.Translate(userId, "permission-denied");

            // Pipeline is stopped in IAsyncAuthorizationFilter if result is non nil
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = message,
                ContentType = "text/plain"
            };


        }
    }
}
