using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.Extensions;
using API.Services.Plus;
using API.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class LicenseController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseController> _logger;
    private readonly ILicenseService _licenseService;
    private readonly IEventHub _eventHub;

    public LicenseController(IUnitOfWork unitOfWork, ILogger<LicenseController> logger,
        ILicenseService licenseService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _licenseService = licenseService;
        _eventHub = eventHub;
    }

    /// <summary>
    /// If the Admin has a license, then some features of Kavita server are unlocked for all users
    /// </summary>
    /// <returns></returns>
    [HttpGet("server-has-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> AdminHasLicense()
    {
        var user = await _unitOfWork.UserRepository.GetDefaultAdminUser();
        return Ok(await _licenseService.HasActiveLicense(user.Id));
    }

    /// <summary>
    /// Checks if the user's license is valid or not
    /// </summary>
    /// <returns></returns>
    [HttpGet("valid-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> HasValidLicense()
    {
        return Ok(await _licenseService.HasActiveLicense(User.GetUserId()));
    }

    /// <summary>
    /// Updates user's license. Returns true if updated and valid
    /// </summary>
    /// <remarks>Caches the result</remarks>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<bool>> UpdateLicense(UpdateLicenseDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        if (string.IsNullOrEmpty(dto.License))
        {
            await _licenseService.RemoveLicenseFromUser(user);
        }
        else
        {
            await _licenseService.AddLicenseToUser(user, dto.License);
        }

        // Send an event to the user so their account updates
        await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate,
            MessageFactory.UserUpdateEvent(user.Id, user.UserName), user.Id);

        return Ok(await _licenseService.HasActiveLicense(user.Id, true));
    }
}
