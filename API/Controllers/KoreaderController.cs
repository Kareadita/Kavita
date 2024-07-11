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

namespace API.Controllers;

#nullable enable

// Koreader uses a different form of athentication. It stores the user name
// and password in headers.
[AllowAnonymous]
public class KoreaderController : BaseApiController
{

    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IKoreaderService _koreaderService;

    public KoreaderController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager,
            ILocalizationService localizationService, IKoreaderService koreaderService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _userManager = userManager;
        _koreaderService = koreaderService;
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
        var userId = await GetUserId(apiKey);
        await _koreaderService.SaveProgress(request.Percentage, request.Document, userId);
        var response = new
        {
            document = request.Document,
            timestamp = DateTime.Now
        };
        return Ok(response);
    }

    [HttpGet("{apiKey}/syncs/progress/{ebookHash}")]
    public async Task<IActionResult> GetProgress(string apiKey, string ebookHash)
    {
        var userId = await GetUserId(apiKey);
        _unitOfWork.VolumeRepository.GetVolumeAsync(1);
        var response = new KoreaderBookDto();
        return Ok(response);

    }


    /// <summary>
    /// Gets the user from the API key
    /// </summary>
    /// <returns></returns>
    private async Task<int> GetUserId(string apiKey)
    {
        try
        {
            return await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        }
        catch
        {
            /* Do nothing */
        }
        throw new KavitaException(await _localizationService.Get("en", "user-doesnt-exist"));
    }
}
