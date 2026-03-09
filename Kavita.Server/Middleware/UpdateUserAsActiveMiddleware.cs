using System;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Store;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Http;

namespace Kavita.Server.Middleware;

/// <summary>
/// If the user is authenticated, will update the <see cref="AppUser.LastActive"/> field.
/// </summary>
/// <remarks>This should be last in the stack of middlewares</remarks>
/// <param name="next"></param>
public class UpdateUserAsActiveMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserContext userContext, IUnitOfWork unitOfWork)
    {
        try
        {
            var userId = userContext.GetUserId();
            if (userId > 0)
            {
                await unitOfWork.UserRepository.UpdateUserAsActive(userId.Value);
            }
        }
        catch (Exception)
        {
            await next(context);
            return;
        }

        await next(context);
    }
}
