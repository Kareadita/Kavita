using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace API.Middleware;

/// <summary>
/// An attribute to restrict endpoint usage to either the user itself (authenticated user == userId) or the
/// requested user is sharing their profile.
/// Returns 400 BadRequest on failure
/// </summary>
/// <param name="queryKey">Defaults to userId</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ProfilePrivacyAttribute(string queryKey = "userId", bool allowMissingUserId = false) : Attribute, IAsyncAuthorizationFilter
{

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userIdString = context.HttpContext.Request.Query[queryKey].FirstOrDefault();
        if (string.IsNullOrEmpty(userIdString))
        {
            if (allowMissingUserId)
            {
                return;
            }

            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentType = "text/plain",
            };
            return;
        }

        var userId = int.Parse(userIdString);
        var user = context.HttpContext.User;
        if (user.GetUserId() == userId)
        {
            return;
        }

        var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var requestedUser = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (requestedUser == null || !requestedUser.UserPreferences.SocialPreferences.ShareProfile)
        {
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentType = "text/plain",
            };
        }
    }
}
