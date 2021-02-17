using System.IO;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class FallbackController : Controller
    {
        private readonly ITaskScheduler _taskScheduler;

        public FallbackController(ITaskScheduler taskScheduler)
        {
            // This is used to load TaskScheduler on startup without having to navigate to a Controller that uses. 
            _taskScheduler = taskScheduler;
        }

        public ActionResult Index()
        {
            return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/HTML");
        }
    }
}