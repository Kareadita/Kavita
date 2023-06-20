using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.Entities.Enums;
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
    /// Checks if the user's license is valid or not
    /// </summary>
    /// <returns></returns>
    [HttpGet("valid-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> HasValidLicense(bool forceCheck = false)
    {
        return Ok(await _licenseService.HasActiveLicense(forceCheck));
    }

    /// <summary>
    /// Updates server license. Returns true if updated and valid
    /// </summary>
    /// <remarks>Caches the result</remarks>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<bool>> UpdateLicense(UpdateLicenseDto dto)
    {
        dto.License = dto.License.Trim();
        if (string.IsNullOrEmpty(dto.License))
        {
            await _licenseService.RemoveLicense();
        }
        else
        {
            await _licenseService.AddLicense(dto.License, dto.Email);
        }

        return Ok(await _licenseService.HasActiveLicense(true));
    }
}
