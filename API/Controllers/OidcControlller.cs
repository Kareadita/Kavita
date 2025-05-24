using System.Threading.Tasks;
using API.Data;
using API.DTOs.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

[AllowAnonymous]
public class OidcController(ILogger<OidcController> logger, IUnitOfWork unitOfWork): BaseApiController
{

    // TODO: Decide what we want to expose here, not really anything useful in it. But the discussion is needed
    //      Public endpoint
    [HttpGet("config")]
    public async Task<ActionResult<OidcConfigDto>> GetOidcConfig()
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settings.OidcConfig);
    }

}
