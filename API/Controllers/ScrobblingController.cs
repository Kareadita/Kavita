using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Account;
using API.DTOs.Scrobbling;
using API.Extensions;
using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;


public class ScrobblingController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobblingService _scrobblingService;

    public ScrobblingController(IUnitOfWork unitOfWork, IScrobblingService scrobblingService)
    {
        _unitOfWork = unitOfWork;
        _scrobblingService = scrobblingService;
    }

    [HttpGet("anilist-token")]
    public async Task<ActionResult> GetAniListToken()
    {
        // Validate the license

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        return Ok(user.AniListAccessToken);
    }

    [HttpPost("update-anilist-token")]
    public async Task<ActionResult> UpdateAniListToken(AniListUpdateDto dto)
    {
        // Validate the license

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        user.AniListAccessToken = dto.Token;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    [HttpGet("token-expired")]
    public async Task<ActionResult<bool>> HasTokenExpired(ScrobbleProvider provider)
    {
        return Ok(await _scrobblingService.HasTokenExpired(User.GetUserId(), provider));
    }

    [HttpGet("scrobble-errors")]
    public async Task<ActionResult<IEnumerable<ScrobbleErrorDto>>> GetScrobbleErrors()
    {
        return Ok(await _unitOfWork.ScrobbleRepository.GetScrobbleErrors());
    }

    [HttpGet("clear-errors")]
    public async Task<ActionResult<IEnumerable<ScrobbleErrorDto>>> ClearScrobbleErrors()
    {
        return Ok(await _unitOfWork.ScrobbleRepository.ClearScrobbleErrors());
    }
}
