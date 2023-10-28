using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// For the Panels app explicitly
/// </summary>
[AllowAnonymous]
public class PanelsController : BaseApiController
{
    private readonly IReaderService _readerService;
    private readonly IUnitOfWork _unitOfWork;

    public PanelsController(IReaderService readerService, IUnitOfWork unitOfWork)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("save-progress")]
    public async Task<ActionResult> SaveProgress(ProgressDto dto, [FromQuery] string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return Unauthorized("ApiKey is required");
        var userId = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
        await _readerService.SaveReadingProgress(dto, userId);
        return Ok();
    }
}
