using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

[AllowAnonymous]
public class HealthController : BaseApiController
{

    [HttpGet]
    public ActionResult GetHealth()
    {
        return Ok("Ok");
    }
}
