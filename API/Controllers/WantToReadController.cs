using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.WantToRead;
using API.Extensions;
using API.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for all things Want To Read
/// </summary>
[Route("api/want-to-read")]
public class WantToReadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public WantToReadController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Return all Series that are in the current logged in user's Want to Read list, filtered
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToRead([FromQuery] UserParams userParams, FilterDto filterDto)
    {
        userParams ??= new UserParams();
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var pagedList = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id, userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);
        return Ok(pagedList);
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

        var existingIds = user.WantToRead.Select(s => s.Id).ToList();
        existingIds.AddRange(dto.SeriesIds);

        var idsToAdd = existingIds.Distinct().ToList();

        var seriesToAdd =  await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(idsToAdd);
        foreach (var series in seriesToAdd)
        {
            user.WantToRead.Add(series);
        }

        if (!_unitOfWork.HasChanges()) return Ok();
        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest("There was an issue updating Read List");
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

        user.WantToRead = user.WantToRead.Where(s => @dto.SeriesIds.Contains(s.Id)).ToList();

        if (!_unitOfWork.HasChanges()) return Ok();
        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest("There was an issue updating Read List");
    }
}
