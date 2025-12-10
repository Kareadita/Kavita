using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Controllers;
#nullable enable

/// <summary>
/// The endpoint to interface with Koreader's Progress Sync plugin.
/// </summary>
/// <remarks>
/// Koreader uses a different form of authentication. It stores the username and password in headers.
/// https://github.com/koreader/koreader/blob/master/plugins/kosync.koplugin/KOSyncClient.lua
/// </remarks>
[AllowAnonymous]
public class KoreaderController : BaseApiController
{

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IKoreaderService _koreaderService;
    private readonly ILogger<KoreaderController> _logger;

    public KoreaderController(IUnitOfWork unitOfWork, ILocalizationService localizationService,
            IKoreaderService koreaderService, ILogger<KoreaderController> logger)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _koreaderService = koreaderService;
        _logger = logger;
    }

    // We won't allow users to be created from Koreader. Rather, they
    // must already have an account.
    /*
    [HttpPost("/users/create")]
    public IActionResult CreateUser(CreateUserRequest request)
    {
    }
    */

    [HttpGet("{apiKey}/users/auth")]
    public async Task<IActionResult> Authenticate(string apiKey)
    {
        var userId = await GetUserId(apiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return Unauthorized();

        return Ok(new { username = user.UserName });
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
            var userId = await GetUserId(apiKey);
            await _koreaderService.SaveProgress(request, userId);

            return Ok(new KoreaderProgressUpdateDto{ Document = request.document, Timestamp = DateTime.UtcNow });
        }
        catch (KavitaException ex)
        {
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
            var userId = await GetUserId(apiKey);
            var response = await _koreaderService.GetProgress(ebookHash, userId);
            _logger.LogDebug("Koreader response progress for User ({UserId}): {Progress}", userId, response.progress.Sanitize());


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
            return BadRequest(ex.Message);
        }
    }

    private async Task<int> GetUserId(string apiKey)
    {
        try
        {
            return await _unitOfWork.UserRepository.GetUserIdByAuthKeyAsync(apiKey);
        }
        catch
        {
            throw new KavitaException(await _localizationService.Get("en", "user-doesnt-exist"));
        }
    }
}
