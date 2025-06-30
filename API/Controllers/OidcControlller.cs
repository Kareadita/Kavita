using System.Threading.Tasks;
using API.Data;
using API.DTOs.Settings;
using API.Services;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class OidcController(ILogger<OidcController> logger, IUnitOfWork unitOfWork,
    IMapper mapper, ISettingsService settingsService): BaseApiController
{

    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<ActionResult<OidcPublicConfigDto>> GetOidcConfig()
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(mapper.Map<OidcPublicConfigDto>(settings.OidcConfig));
    }

    [Authorize("RequireAdminRole")]
    [HttpPost("is-valid-authority")]
    public async Task<ActionResult<bool>> IsValidAuthority([FromBody] IsValidAuthorityBody authority)
    {
        return Ok(await settingsService.IsValidAuthority(authority.Authority));
    }

    public class IsValidAuthorityBody
    {
        public string Authority { get; set; }
    }

}
