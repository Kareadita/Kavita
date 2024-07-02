using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
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


}
