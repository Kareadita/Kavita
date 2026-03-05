using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.WantToRead;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;
/// <summary>
/// Responsible for all things Want To Read
/// </summary>
[Route("api/want-to-read")]
public class WantToReadController(
    IUnitOfWork unitOfWork,
    IScrobblingService scrobblingService,
    ILocalizationService localizationService,
    ISeriesService seriesService)
    : BaseApiController
{
    /// <summary>
    /// Return all Series that are in the current logged in user's Want to Read list, filtered
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <param name="userId">Optional user id to request the OnDeck for someone else. They must have profile sharing enabled when doing so</param>
    /// <returns></returns>
    [HttpPost("v2")]
    [ProfilePrivacy(allowMissingUserId: true)]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToReadV2([FromQuery] UserParams? userParams, FilterV2Dto filterDto, [FromQuery] int? userId = null)
    {
        var wantToReadForUser = userId ?? UserId;
        userParams ??= new UserParams();

        // Add profile privacy filter
        filterDto.Statements.AddRange(await seriesService.GetProfilePrivacyStatements(wantToReadForUser, UserId));

        var pagedList = await unitOfWork.SeriesRepository.GetWantToReadForUserV2Async(wantToReadForUser, userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        return Ok(pagedList);
    }

    [HttpGet]
    [SeriesAccess]
    public async Task<ActionResult<bool>> IsSeriesInWantToRead([FromQuery] int seriesId)
    {
        return Ok(await unitOfWork.SeriesRepository.IsSeriesInWantToRead(UserId, seriesId));
    }

    /// <summary>
    /// Given a list of Series Ids, add them to the current logged in user's Want To Read list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("add-series")]
    public async Task<ActionResult> AddSeries(UpdateWantToReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
            AppUserIncludes.WantToRead);
        if (user == null) return Unauthorized();

        var existingIds = user.WantToRead.Select(s => s.SeriesId).ToList();
        var idsToAdd = dto.SeriesIds.Except(existingIds);

        foreach (var id in idsToAdd)
        {
            user.WantToRead.Add(new AppUserWantToRead()
            {
                SeriesId = id
            });
        }

        if (!unitOfWork.HasChanges()) return Ok();
        if (await unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => scrobblingService.ScrobbleWantToReadUpdate(user.Id, sId, true));
            }
            return Ok();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-reading-list-update"));
    }

    /// <summary>
    /// Given a list of Series Ids, remove them from the current logged in user's Want To Read list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("remove-series")]
    public async Task<ActionResult> RemoveSeries(UpdateWantToReadDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!,
            AppUserIncludes.WantToRead);
        if (user == null) return Unauthorized();

        user.WantToRead = user.WantToRead
            .Where(s => !dto.SeriesIds.Contains(s.SeriesId))
            .ToList();

        if (!unitOfWork.HasChanges()) return Ok();
        if (await unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => scrobblingService.ScrobbleWantToReadUpdate(user.Id, sId, false));
            }

            return Ok();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-reading-list-update"));
    }
}
