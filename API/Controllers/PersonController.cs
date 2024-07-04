using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class PersonController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public PersonController(IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    // [HttpGet("{personId}")]
    // public async Task<ActionResult<PersonDto>> GetPerson(int personId)
    // {
    //     return Ok(await _unitOfWork.PersonRepository.GetPersonDtoAsync(personId, User.GetUserId()));
    // }
    //
    // [HttpGet("{personId}/roles")]
    // public async Task<ActionResult<IEnumerable<PersonRole>>> GetRolesForPerson(int personId)
    // {
    //     return Ok(await _unitOfWork.PersonRepository.GetRolesForPerson(personId, User.GetUserId()));
    // }

    [HttpGet]
    public async Task<ActionResult<PersonDto>> GetPersonByName(string name)
    {
        return Ok(await _unitOfWork.PersonRepository.GetPersonDtoByName(name, User.GetUserId()));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<PersonRole>>> GetRolesForPersonByName(string name)
    {
        return Ok(await _unitOfWork.PersonRepository.GetRolesForPersonByName(name, User.GetUserId()));
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

    /// <summary>
    /// Updates the Person
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult<PersonDto>> UpdatePerson(UpdatePersonDto dto)
    {
        var person = await _unitOfWork.PersonRepository.GetPersonById(dto.Id);
        if (person == null) return BadRequest(_localizationService.Translate(User.GetUserId(), "person-doesnt-exist"));

        person.CoverImageLocked = dto.CoverImageLocked;
        if (dto.MalId is > 0)
        {
            person.MalId = (long) dto.MalId;
        }

        return Ok();
    }


}
