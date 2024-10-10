﻿using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class PersonController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IMapper _mapper;

    public PersonController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _mapper = mapper;
    }


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
        // This needs to get all people and update them equally
        var person = await _unitOfWork.PersonRepository.GetPersonById(dto.Id);
        if (person == null) return BadRequest(_localizationService.Translate(User.GetUserId(), "person-doesnt-exist"));

        dto.Description ??= string.Empty;
        person.Description = dto.Description;
        person.CoverImageLocked = dto.CoverImageLocked;
        if (dto.MalId is > 0)
        {
            person.MalId = (long) dto.MalId;
        }
        if (dto.AniListId is > 0)
        {
            person.AniListId = (int) dto.AniListId;
        }

        if (!string.IsNullOrEmpty(dto.HardcoverId?.Trim()))
        {
            person.HardcoverId = dto.HardcoverId.Trim();
        }
        if (!string.IsNullOrEmpty(dto.Asin?.Trim()))
        {
            person.Asin = dto.Asin.Trim();
        }

        _unitOfWork.PersonRepository.Update(person);
        await _unitOfWork.CommitAsync();

        return Ok(_mapper.Map<PersonDto>(person));
    }


}