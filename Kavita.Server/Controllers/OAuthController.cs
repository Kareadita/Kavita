using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.DTOs.Settings;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[KPlus]
public class OAuthController(IOAuthService oAuthService, IUnitOfWork unitOfWork, IKavitaPlusApiService kavitaPlusApiService): BaseApiController
{

    /// <summary>
    /// Start the OAuth flow for the given upstream, redirect (302!) to KavitaPlus
    /// </summary>
    /// <param name="upstream"></param>
    /// <returns></returns>
    [HttpGet("start")]
    public async Task<IActionResult> StartFlow([FromQuery] OAuthUpstream upstream)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.AuthKeys);
        if (user == null) return Unauthorized();

        if (upstream == OAuthUpstream.Discord && !UserContext.HasRole(PolicyConstants.AdminRole))
        {
            return BadRequest();
        }

        var apiKey = user.GetOpdsAuthKey();

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var instanceUrl = GetInstanceUrl(HttpContext.Request, serverSettings);

        var jwt = await kavitaPlusApiService.StartOAuthFlow(upstream, instanceUrl, apiKey, HttpContext.RequestAborted);
        if (!jwt.IsSuccess)
        {
            return BadRequest(jwt.ErrorMessage);
        }

        var redirectUrl = (Configuration.KavitaPlusApiUrl + "/token-relay/continue-flow")
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
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] OAuthUpstream upstream, [FromQuery] string token,
        [FromQuery] string? refreshToken = null)
    {
        await oAuthService.HandleCallback(upstream, token, refreshToken);

        if (upstream == OAuthUpstream.Discord)
        {
            return Redirect("/settings?loading=true#admin-kavitaplus");
        }

        return Redirect("/settings?loading=true#scrobble-settings");
    }

    private static string GetInstanceUrl(HttpRequest request, ServerSettingDto serverSettings)
    {
        var origin = string.IsNullOrEmpty(serverSettings.HostName)
            ?  request.Scheme + "://" + request.Host.Value
            : serverSettings.HostName.TrimEnd('/');

        origin += string.IsNullOrEmpty(serverSettings.BaseUrl) ? string.Empty : serverSettings.BaseUrl.TrimEnd('/');

        return origin + "/";
    }

}
