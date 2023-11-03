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
    private readonly ITaskScheduler _taskScheduler;

    public FallbackController(ITaskScheduler taskScheduler)
    {
        // This is used to load TaskScheduler on startup without having to navigate to a Controller that uses.
        _taskScheduler = taskScheduler;
    }

    public PhysicalFileResult Index()
    {
        return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/HTML");
    }
}

