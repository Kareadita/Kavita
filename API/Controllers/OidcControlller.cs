using System.Threading.Tasks;
using API.Data;
using API.DTOs.Settings;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

[AllowAnonymous]
public class OidcController(ILogger<OidcController> logger, IUnitOfWork unitOfWork, IMapper mapper): BaseApiController
{

    [HttpGet("config")]
    public async Task<ActionResult<OidcPublicConfigDto>> GetOidcConfig()
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(mapper.Map<OidcPublicConfigDto>(settings.OidcConfig));
    }

}
