using System.IO;
using Kavita.API.Attributes;
using Kavita.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[AllowAnonymous]
public class FallbackController : Controller
{

    [SkipDeviceTracking]
    public IActionResult Index()
    {
        if (HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            return NotFound();
        }

        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/HTML");
    }
}

