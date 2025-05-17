#nullable enable
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class ReadingProfileController(ILogger<ReadingProfileController> logger, IUnitOfWork unitOfWork,
    IReadingProfileService readingProfileService): BaseApiController
{

    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series -> Library -> Default
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("{seriesId}")]
    public async Task<ActionResult<UserReadingProfileDto?>> GetProfileForSeries(int seriesId)
    {
        return Ok(await readingProfileService.GetReadingProfileForSeries(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Update, or create the given profile
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="seriesCtx">
    /// Optionally, from which series the update is called.
    /// If set, will delete the implicit reading profile if it exists
    /// </param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult> UpdateReadingProfile([FromBody] UserReadingProfileDto dto, [FromQuery] int? seriesCtx)
    {
        if (seriesCtx.HasValue)
        {
            await readingProfileService.DeleteImplicitForSeries(User.GetUserId(), seriesCtx.Value);
        }


        var success = await readingProfileService.UpdateReadingProfile(User.GetUserId(), dto);
        if (!success) return BadRequest();

        return Ok();
    }

    /// <summary>
    /// Update the implicit reading profile for a series, creates one if none exists
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("series")]
    public async Task<ActionResult> UpdateReadingProfileForSeries([FromBody] UserReadingProfileDto dto, [FromQuery] int seriesId)
    {
        var success = await readingProfileService.UpdateImplicitReadingProfile(User.GetUserId(), seriesId, dto);
        if (!success) return BadRequest();

        return Ok();
    }

}
