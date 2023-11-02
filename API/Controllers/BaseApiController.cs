using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BaseApiController : ControllerBase
{
}
