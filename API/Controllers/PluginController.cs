using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Misc;
using API.Entities.Enums;
using API.Middleware;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

[SkipDeviceTracking]
public class PluginController(IUnitOfWork unitOfWork, ITokenService tokenService, ILogger<PluginController> logger)
    : BaseApiController
{
    /// <summary>
    /// Authenticate with the Server given an auth key. This will log you in by returning the user object and the JWT token.
    /// </summary>
    /// <remarks>This API is not fully built out and may require more information in later releases</remarks>
    /// <remarks>This will log unauthorized requests to Security log</remarks>
    /// <param name="apiKey">Auth key which will be used to authenticate and return a valid user token back</param>
    /// <param name="pluginName">Name of the Plugin</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("authenticate")]
    public async Task<ActionResult<UserDto>> Authenticate([Required] string apiKey, [Required] string pluginName)
    {
        // NOTE: In order to log information about plugins, we need some Plugin Description information for each request
        // Should log into the access table so we can tell the user
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent;

        var userId = await unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
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
        logger.LogInformation("Plugin {PluginName} has authenticated with {UserName} ({AppUserId})'s API Key", pluginName.Replace(Environment.NewLine, string.Empty), user!.UserName, userId);

        return new UserDto
        {
            Username = user.UserName!,
            Token = await tokenService.CreateToken(user),
            RefreshToken = await tokenService.CreateRefreshToken(user),
            ApiKey = apiKey,
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
        var userId = await unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        if (userId <= 0) throw new KavitaUnauthenticatedUserException();
        return Ok((await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value);
    }

    /// <summary>
    /// Returns the expiration (UTC) of the authenticated Auth key (or null if none set)
    /// </summary>
    /// <remarks>Will always return null if the Auth Key does not belong to this account</remarks>
    /// <returns></returns>
    [HttpGet("authkey-expires")]
    public async Task<ActionResult<DateTime?>> GetAuthKeyExpiration()
    {
        var authKey = AuthKey;
        if (string.IsNullOrEmpty(authKey))
            return BadRequest();

        var exp = await unitOfWork.UserRepository.GetAuthKeyExpiration(authKey, UserId);

        return Ok(new { ExpiresAt = exp?.ToUniversalTime() });
    }


    /// <summary>
    /// Parse a string and return Parsed information from it. Does not support any directory fallback parsing
    /// </summary>
    /// <param name="name">String to parse</param>
    /// <param name="libraryType">Determines the set of pattern matching to use</param>
    /// <returns></returns>
    [HttpGet("parse")]
    public ActionResult<ParseResultDto> Parse([FromQuery] [StringLength(1000)] string name, [FromQuery] LibraryType libraryType)
    {
        try
        {
            var result = ParseText(name, libraryType);

            return Ok(result);
        }
        catch (RegexMatchTimeoutException)
        {
            return BadRequest("Input could not be parsed in allowed time");
        }
        catch (Exception)
        {
            return BadRequest("Failed to parse input");
        }
    }

    private static ParseResultDto ParseText(string name, LibraryType libraryType)
    {
        var result = new ParseResultDto
        {
            SeriesName = Parser.ParseSeries(name, libraryType),
            SeriesYear = Parser.ParseYear(name)
        };
        var chapterRange = Parser.ParseChapter(name, libraryType);
        result.MinChapterNumber = Parser.MinNumberFromRange(chapterRange);
        result.MaxChapterNumber = Parser.MaxNumberFromRange(chapterRange);
        var volumeRange = Parser.ParseVolume(name, libraryType);
        result.MinVolumeNumber = Parser.MinNumberFromRange(volumeRange);
        result.MaxVolumeNumber = Parser.MaxNumberFromRange(volumeRange);
        return result;
    }

    [HttpPost("parse-bulk")]
    public ActionResult<ParseBulkResponseDto> ParseBulk(ParseBulkRequestDto dto, CancellationToken cancellationToken)
    {
        if (dto.Names.Count > 100)
        {
            return BadRequest("Only 100 items can be processed at once");
        }

        var result = new ParseBulkResponseDto();

        var successfulParses = result.Results;
        var errorParses = result.Errors;
        foreach (var name in dto.Names)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (name.Length > 1000)
            {
                errorParses.Add(name, "Length > 1000 characters");
                continue;
            }

            try
            {
                successfulParses.Add(name, ParseText(name, dto.LibraryType));
            }
            catch (RegexMatchTimeoutException)
            {
                errorParses.Add(name, "Input could not be parsed in allowed time");
            }
            catch (Exception)
            {
                errorParses.Add(name, "Failed to parse input");
            }
        }

        return Ok(result);
    }
}
