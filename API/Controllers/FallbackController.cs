using System.IO;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

[AllowAnonymous]
public class FallbackController : Controller
{
    // ReSharper disable once S4487
    // ReSharper disable once NotAccessedField.Local
#pragma warning disable S4487
    private readonly ITaskScheduler _taskScheduler;
#pragma warning restore S4487

    public FallbackController(ITaskScheduler taskScheduler)
    {
        // This is used to load TaskScheduler on startup without having to navigate to a Controller that uses.
        _taskScheduler = taskScheduler; // TODO: Validate if this is needed as a DI anymore since we have a HostedStartupService
    }

    public IActionResult Index()
    {
        if (HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            return NotFound();
        }

        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/HTML");
    }
}

