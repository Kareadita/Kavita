using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.Entities.Enums;
using API.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class LicenseController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseController> _logger;
    private readonly ILicenseService _licenseService;

    public LicenseController(IUnitOfWork unitOfWork, ILogger<LicenseController> logger,
        ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _licenseService = licenseService;
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
    /// Has any license
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpGet("has-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> HasLicense()
    {
        return Ok(!string.IsNullOrEmpty(
            (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value));
    }

    [Authorize("RequireAdminRole")]
    [HttpDelete]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult> RemoveLicense()
    {
        var setting = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        setting.Value = null;
        _unitOfWork.SettingsRepository.Update(setting);
        await _unitOfWork.CommitAsync();
        return Ok();
    }

    /// <summary>
    /// Updates server license. Returns true if updated and valid
    /// </summary>
    /// <remarks>Caches the result</remarks>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
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
