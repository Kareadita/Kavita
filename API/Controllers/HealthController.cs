using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[AllowAnonymous]
public class HealthController : BaseApiController
{

    [HttpGet]
    public ActionResult GetHealth()
    {
        return Ok("Ok");
    }
}
