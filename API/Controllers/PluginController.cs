using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class PluginController(IUnitOfWork unitOfWork, ITokenService tokenService, ILogger<PluginController> logger)
    : BaseApiController
{
    /// <summary>
    /// Authenticate with the Server given an apiKey. This will log you in by returning the user object and the JWT token.
    /// </summary>
    /// <remarks>This API is not fully built out and may require more information in later releases</remarks>
    /// <remarks>This will log unauthorized requests to Security log</remarks>
    /// <param name="apiKey">API key which will be used to authenticate and return a valid user token back</param>
    /// <param name="pluginName">Name of the Plugin</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("authenticate")]
    public async Task<ActionResult<UserDto>> Authenticate([Required] string apiKey, [Required] string pluginName)
    {
        // NOTE: In order to log information about plugins, we need some Plugin Description information for each request
        // Should log into access table so we can tell the user
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent;
        var userId = await unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId <= 0)
        {
            logger.LogInformation("A Plugin ({PluginName}) tried to authenticate with an apiKey that doesn't match. Information {@Information}", pluginName.Replace(Environment.NewLine, string.Empty), new
            {
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ApiKey = apiKey
            });
            throw new KavitaUnauthenticatedUserException();
        }
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
        logger.LogInformation("Plugin {PluginName} has authenticated with {UserName} ({UserId})'s API Key", pluginName.Replace(Environment.NewLine, string.Empty), user!.UserName, userId);

        return new UserDto
        {
            Username = user.UserName!,
            Token = await tokenService.CreateToken(user),
            RefreshToken = await tokenService.CreateRefreshToken(user),
            ApiKey = user.ApiKey,
            KavitaVersion = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value
        };
    }

    /// <summary>
    /// Returns the version of the Kavita install
    /// </summary>
    /// <remarks>This will log unauthorized requests to Security log</remarks>
    /// <param name="apiKey">Required for authenticating to get result</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("version")]
    public async Task<ActionResult<string>> GetVersion([Required] string apiKey)
    {
        var userId = await unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        if (userId <= 0) throw new KavitaUnauthenticatedUserException();
        return Ok((await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value);
    }
}
