using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.WantToRead;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Plus;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for all things Want To Read
/// </summary>
[Route("api/want-to-read")]
public class WantToReadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILocalizationService _localizationService;

    public WantToReadController(IUnitOfWork unitOfWork, IScrobblingService scrobblingService,
        ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _scrobblingService = scrobblingService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Return all Series that are in the current logged in user's Want to Read list, filtered (deprecated, use v2)
    /// </summary>
    /// <remarks>This will be removed in v0.8.x</remarks>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost]
    [Obsolete("use v2 instead")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToRead([FromQuery] UserParams? userParams, FilterDto filterDto)
    {
        userParams ??= new UserParams();
        var pagedList = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(User.GetUserId(), userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(User.GetUserId(), pagedList);

        return Ok(pagedList);
    }

    /// <summary>
    /// Return all Series that are in the current logged in user's Want to Read list, filtered
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("v2")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToReadV2([FromQuery] UserParams? userParams, FilterV2Dto filterDto)
    {
        userParams ??= new UserParams();
        var pagedList = await _unitOfWork.SeriesRepository.GetWantToReadForUserV2Async(User.GetUserId(), userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(User.GetUserId(), pagedList);

        return Ok(pagedList);
    }

    [HttpGet]
    public async Task<ActionResult<bool>> IsSeriesInWantToRead([FromQuery] int seriesId)
    {
        return Ok(await _unitOfWork.SeriesRepository.IsSeriesInWantToRead(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Given a list of Series Ids, add them to the current logged in user's Want To Read list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("add-series")]
    public async Task<ActionResult> AddSeries(UpdateWantToReadDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
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

        if (!_unitOfWork.HasChanges()) return Ok();
        if (await _unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleWantToReadUpdate(user.Id, sId, true));
            }
            return Ok();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-reading-list-update"));
    }

    /// <summary>
    /// Given a list of Series Ids, remove them from the current logged in user's Want To Read list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("remove-series")]
    public async Task<ActionResult> RemoveSeries(UpdateWantToReadDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
            AppUserIncludes.WantToRead);
        if (user == null) return Unauthorized();

        user.WantToRead = user.WantToRead
            .Where(s => !dto.SeriesIds.Contains(s.SeriesId))
            .ToList();

        if (!_unitOfWork.HasChanges()) return Ok();
        if (await _unitOfWork.CommitAsync())
        {
            foreach (var sId in dto.SeriesIds)
            {
                BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleWantToReadUpdate(user.Id, sId, false));
            }

            return Ok();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-reading-list-update"));
    }
}
