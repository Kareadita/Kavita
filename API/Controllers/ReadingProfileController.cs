#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

[Route("api/reading-profile")]
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
        return Ok(await unitOfWork.AppUserReadingProfileRepository.GetProfilesDtoForUser(UserId, true));
    }

    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series -> Library -> Default
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="skipImplicit"></param>
    /// <returns></returns>
    [HttpGet("{seriesId:int}")]
    public async Task<ActionResult<UserReadingProfileDto>> GetProfileForSeries(int seriesId, [FromQuery] bool skipImplicit)
    {
        return Ok(await readingProfileService.GetReadingProfileDtoForSeries(UserId, seriesId, skipImplicit));
    }

    /// <summary>
    /// Returns the (potential) Reading Profile bound to the library
    /// </summary>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpGet("library")]
    public async Task<ActionResult<UserReadingProfileDto?>> GetProfileForLibrary(int libraryId)
    {
        return Ok(await readingProfileService.GetReadingProfileDtoForLibrary(UserId, libraryId));
    }

    /// <summary>
    /// Creates a new reading profile for the current user
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<UserReadingProfileDto>> CreateReadingProfile([FromBody] UserReadingProfileDto dto)
    {
        return Ok(await readingProfileService.CreateReadingProfile(UserId, dto));
    }

    /// <summary>
    /// Promotes the implicit profile to a user profile. Removes the series from other profiles
    /// </summary>
    /// <param name="profileId"></param>
    /// <returns></returns>
    [HttpPost("promote")]
    public async Task<ActionResult<UserReadingProfileDto>> PromoteImplicitReadingProfile([FromQuery] int profileId)
    {
        return Ok(await readingProfileService.PromoteImplicitProfile(UserId, profileId));
    }

    /// <summary>
    /// Update the implicit reading profile for a series, creates one if none exists
    /// </summary>
    /// <remarks>Any modification to the reader settings during reading will create an implicit profile. Use "update-parent" to save to the bound series profile.</remarks>
    /// <param name="dto"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("series")]
    public async Task<ActionResult<UserReadingProfileDto>> UpdateReadingProfileForSeries([FromBody] UserReadingProfileDto dto, [FromQuery] int seriesId)
    {
        var updatedProfile = await readingProfileService.UpdateImplicitReadingProfile(UserId, seriesId, dto);
        return Ok(updatedProfile);
    }

    /// <summary>
    /// Updates the non-implicit reading profile for the given series, and removes implicit profiles
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("update-parent")]
    public async Task<ActionResult<UserReadingProfileDto>> UpdateParentProfileForSeries([FromBody] UserReadingProfileDto dto, [FromQuery] int seriesId)
    {
        var newParentProfile = await readingProfileService.UpdateParent(UserId, seriesId, dto);
        return Ok(newParentProfile);
    }

    /// <summary>
    /// Updates the given reading profile, must belong to the current user
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>The updated reading profile</returns>
    /// <remarks>
    /// This does not update connected series and libraries.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<UserReadingProfileDto>> UpdateReadingProfile(UserReadingProfileDto dto)
    {
        return Ok(await readingProfileService.UpdateReadingProfile(UserId, dto));
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
        await readingProfileService.DeleteReadingProfile(UserId, profileId);
        return Ok();
    }

    /// <summary>
    /// Sets the reading profile for a given series, removes the old one
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    [HttpPost("series/{seriesId:int}")]
    public async Task<IActionResult> AddProfileToSeries(int seriesId, [FromQuery] int profileId)
    {
        await readingProfileService.AddProfileToSeries(UserId, profileId, seriesId);
        return Ok();
    }

    /// <summary>
    /// Clears the reading profile for the given series for the currently logged-in user
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpDelete("series/{seriesId:int}")]
    public async Task<IActionResult> ClearSeriesProfile(int seriesId)
    {
        await readingProfileService.ClearSeriesProfile(UserId, seriesId);
        return Ok();
    }

    /// <summary>
    /// Sets the reading profile for a given library, removes the old one
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    [HttpPost("library/{libraryId:int}")]
    public async Task<IActionResult> AddProfileToLibrary(int libraryId, [FromQuery] int profileId)
    {
        await readingProfileService.AddProfileToLibrary(UserId, profileId, libraryId);
        return Ok();
    }

    /// <summary>
    /// Clears the reading profile for the given library for the currently logged-in user
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    [HttpDelete("library/{libraryId:int}")]
    public async Task<IActionResult> ClearLibraryProfile(int libraryId)
    {
        await readingProfileService.ClearLibraryProfile(UserId, libraryId);
        return Ok();
    }

    /// <summary>
    /// Assigns the reading profile to all passes series, and deletes their implicit profiles
    /// </summary>
    /// <param name="profileId"></param>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAddReadingProfile([FromQuery] int profileId, [FromBody] IList<int> seriesIds)
    {
        await readingProfileService.BulkAddProfileToSeries(UserId, profileId, seriesIds);
        return Ok();
    }

}
