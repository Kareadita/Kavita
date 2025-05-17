#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class ReadingProfileController(ILogger<ReadingProfileController> logger, IUnitOfWork unitOfWork,
    IReadingProfileService readingProfileService): BaseApiController
{

    /// <summary>
    /// Gets all non-implicit reading profiles for a user
    /// </summary>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<ActionResult<IList<UserReadingProfileDto>>> GetAllReadingProfiles()
    {
        return Ok(await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(User.GetUserId(), true));
    }

    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series -> Library -> Default
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("{seriesId}")]
    public async Task<ActionResult<UserReadingProfileDto>> GetProfileForSeries(int seriesId)
    {
        return Ok(await readingProfileService.GetReadingProfileForSeries(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Updates the given reading profile, must belong to the current user
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
    /// Creates a new reading profile for the current user
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<UserReadingProfileDto>> CreateReadingProfile([FromBody] UserReadingProfileDto dto)
    {
        return Ok(await readingProfileService.CreateReadingProfile(User.GetUserId(), dto));
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

    /// <summary>
    /// Sets the given profile as the global default
    /// </summary>
    /// <param name="profileId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [HttpPost("set-default")]
    public async Task<IActionResult> SetDefault([FromQuery] int profileId)
    {
        await readingProfileService.SetDefaultReadingProfile(User.GetUserId(), profileId);
        return Ok();
    }

    /// <summary>
    /// Deletes the given profile, requires the profile to belong to the logged-in user
    /// </summary>
    /// <param name="profileId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [HttpDelete]
    public async Task<IActionResult> DeleteReadingProfile([FromQuery] int profileId)
    {
        await readingProfileService.DeleteReadingProfile(User.GetUserId(), profileId);
        return Ok();
    }

}
