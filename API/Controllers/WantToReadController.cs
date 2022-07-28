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

[Route("api/want-to-read")]
public class WantToReadController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public WantToReadController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpPost]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToRead([FromQuery] UserParams userParams, FilterDto filterDto)
    {
        userParams ??= new UserParams();
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var pagedList = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id, userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);
        return Ok(pagedList);
    }

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
