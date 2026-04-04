using System;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Koreader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

/// <summary>
/// The endpoint to interface with Koreader's Progress Sync plugin.
/// </summary>
/// <remarks>
/// Koreader uses a different form of authentication. It stores the username and password in headers.
/// https://github.com/koreader/koreader/blob/master/plugins/kosync.koplugin/KOSyncClient.lua
/// </remarks>
public class KoreaderController(IKoreaderService koreaderService, ILogger<KoreaderController> logger)
    : BaseApiController
{
    [HttpGet("{apiKey}/users/auth")]
    public IActionResult Authenticate(string apiKey)
    {
        return Ok(new { username = Username });
    }

    /// <summary>
    /// Syncs book progress with Kavita. Will attempt to save the underlying reader position if possible.
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPut("{apiKey}/syncs/progress")]
    public async Task<ActionResult<KoreaderProgressUpdateDto>> UpdateProgress(string apiKey, KoreaderBookDto request)
    {
        try
        {
            await koreaderService.SaveProgress(request, UserId);

            return Ok(new KoreaderProgressUpdateDto{ Document = request.document, Timestamp = DateTime.UtcNow });
        }
        catch (KavitaException ex)
        {
            logger.LogDebug(ex, "Koreader error saving progress for User ({UserName})", Username);

            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets book progress from Kavita, if not found will return a 400
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="ebookHash"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/syncs/progress/{ebookHash}")]
    public async Task<IActionResult> GetProgress(string apiKey, string ebookHash)
    {
        try
        {
            var response = await koreaderService.GetProgress(ebookHash, UserId);
            logger.LogDebug("Koreader response progress for User ({UserName}): {Progress}", Username, response.progress.Sanitize());


            // We must pack this manually for Koreader due to a bug in their code: https://github.com/koreader/koreader/issues/13629
            var json = System.Text.Json.JsonSerializer.Serialize(response);

            return new ContentResult()
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (KavitaException ex)
        {
            logger.LogDebug(ex, "Koreader error getting progress for User ({UserName})", Username);

            return BadRequest(ex.Message);
        }
    }
}
