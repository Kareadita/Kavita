using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.DTOs.Settings;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

public class OAuthController(IOAuthService oAuthService, IUnitOfWork unitOfWork, IKavitaPlusApiService kavitaPlusApiService): BaseApiController
{

    /// <summary>
    /// Start the OAuth flow for the given upstream, redirect (302!) to KavitaPlus
    /// </summary>
    /// <param name="upstream"></param>
    /// <returns></returns>
    [KPlus]
    [HttpGet("start")]
    public async Task<IActionResult> StartFlow([FromQuery] OAuthUpstream upstream)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.AuthKeys);
        if (user == null) return Unauthorized();

        var apiKey = user.GetOpdsAuthKey();

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var instanceUrl = GetInstanceUrl(HttpContext.Request, serverSettings);

        var jwt = await kavitaPlusApiService.StartOAuthFlow(upstream, instanceUrl, apiKey, HttpContext.RequestAborted);
        if (!jwt.IsSuccess)
        {
            return BadRequest(jwt.ErrorMessage);
        }

        var redirectUrl = (Configuration.KavitaPlusApiUrl + "/TokenRelay/continue-flow")
            .SetQueryParam("token", jwt.Data);

        return Redirect(redirectUrl.ToString());
    }


    /// <summary>
    /// Callback from KavitaPlus.
    /// </summary>
    /// <param name="upstream"></param>
    /// <param name="token"></param>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    [KPlus]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] OAuthUpstream upstream, [FromQuery] string token,
        [FromQuery] string? refreshToken = null)
    {
        await oAuthService.HandleCallback(upstream, token, refreshToken);

        if (upstream == OAuthUpstream.Discord)
        {
            return Redirect("/settings#admin-kavitaplus");
        }

        return Redirect("/settings#scrobble-settings");
    }

    private static string GetInstanceUrl(HttpRequest request, ServerSettingDto serverSettings)
    {
        var origin =string.IsNullOrEmpty(serverSettings.HostName)
            ?  request.Scheme + "://" + request.Host.Value
            : serverSettings.HostName.TrimEnd('/');

        origin += string.IsNullOrEmpty(serverSettings.BaseUrl) ? "" : serverSettings.BaseUrl.TrimEnd('/');

        return origin + "/";
    }

}
