using System.Threading.Tasks;
using API.Data;
using API.DTOs.Account;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;


public class ScrobblingController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public ScrobblingController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
}
