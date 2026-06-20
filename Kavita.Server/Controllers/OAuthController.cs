using System;
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
using Kavita.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class OAuthController(
    ILogger<OAuthController> logger,
    IOAuthService oAuthService,
    IUnitOfWork unitOfWork,
    IKavitaPlusApiService kavitaPlusApiService,
    IDataProtectionProvider dataProtectionProvider): BaseApiController
{

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(KavitaPlusApiService.ApiKeyDataProtectorName);

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
    /// <param name="apiKey">Encrypted ApiKey</param>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    [KPlus(true)]
    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] OAuthUpstream upstream, [FromQuery] string token,
        [FromQuery] string apiKey, [FromQuery] string? refreshToken = null)
    {
        try
        {
            apiKey = _dataProtector.Unprotect(apiKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error decrypting API key");
            return BadRequest();
        }

        var user = await unitOfWork.UserRepository.GetUserByAuthKey(apiKey, HttpContext.RequestAborted);
        if (user == null)
        {
            return Unauthorized();
        }

        await oAuthService.HandleCallback(user, upstream, token, refreshToken);

        if (upstream == OAuthUpstream.Discord)
        {
            return Redirect($"{Configuration.BaseUrl}settings?loading=true#admin-kavitaplus");
        }

        return Redirect($"{Configuration.BaseUrl}settings?loading=true#scrobble-settings");
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
