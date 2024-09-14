using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Koreader;
using API.Entities;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using static System.Net.WebRequestMethods;

namespace API.Controllers;

#nullable enable
/// <summary>
/// The endpoint to interface with Koreader's Progress Sync plugin.
/// </summary>
/// <remarks>
/// Koreader uses a different form of athentication. It stores the user name
/// and password in headers.
/// </remarks>
/// <see cref="https://github.com/koreader/koreader/blob/master/plugins/kosync.koplugin/KOSyncClient.lua"/>
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

        return Ok(new { username = user.UserName });
    }


    [HttpPut("{apiKey}/syncs/progress")]
    public async Task<IActionResult> UpdateProgress(string apiKey, KoreaderBookDto request)
    {
        _logger.LogDebug("Koreader sync progress: {Progress}", request.Progress);
        var userId = await GetUserId(apiKey);
        await _koreaderService.SaveProgress(request, userId);
        
        return Ok(new { document = request.Document, timestamp = DateTime.UtcNow });
    }

    [HttpGet("{apiKey}/syncs/progress/{ebookHash}")]
    public async Task<IActionResult> GetProgress(string apiKey, string ebookHash)
    {
        var userId = await GetUserId(apiKey);
        var response = await _koreaderService.GetProgress(ebookHash, userId);
        _logger.LogDebug("Koreader response progress: {Progress}", response.Progress);

        return Ok(response);
    }


    /// <summary>
    /// Gets the user from the API key
    /// </summary>
    /// <returns>The user's Id</returns>
    private async Task<int> GetUserId(string apiKey)
    {
        try
        {
            return await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        }
        catch
        {
            throw new KavitaException(await _localizationService.Get("en", "user-doesnt-exist"));
        }
    }
}
