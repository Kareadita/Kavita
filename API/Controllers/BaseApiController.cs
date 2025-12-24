using API.Middleware;
using API.Services.Store;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace API.Controllers;

#nullable enable

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current user context. Available in all derived controllers.
    /// </summary>
    protected IUserContext UserContext =>
        field ??= HttpContext.RequestServices.GetRequiredService<IUserContext>();

    /// <summary>
    /// Gets the current authenticated user's ID.
    /// Throws if user is not authenticated.
    /// </summary>
    protected int UserId => UserContext.GetUserIdOrThrow();

    /// <summary>
    /// Gets the current authenticated user's username.
    /// </summary>
    /// <remarks>Warning! Username's can contain .. and /, do not use folders or filenames explicitly with the Username</remarks>
    protected string? Username => UserContext.GetUsername();

}
