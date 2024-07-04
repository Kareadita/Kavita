using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class PersonController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public PersonController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("{personId}")]
    public async Task<ActionResult<PersonDto>> GetPerson(int personId)
    {
        return Ok(await _unitOfWork.PersonRepository.GetPersonDtoAsync(personId, User.GetUserId()));
    }

    [HttpGet("{personId}/roles")]
    public async Task<ActionResult<IEnumerable<PersonRole>>> GetRolesForPerson(int personId)
    {
        return Ok(await _unitOfWork.PersonRepository.GetRolesForPerson(personId, User.GetUserId()));
    }

    /// <summary>
    /// Returns a list of authors for browsing
    /// </summary>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("authors")]
    public async Task<ActionResult<PagedList<BrowsePersonDto>>> GetAuthorsForBrowse([FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var list = await _unitOfWork.PersonRepository.GetAllWritersAndSeriesCount(User.GetUserId(), userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);
        return Ok(list);
    }


}
