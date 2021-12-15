using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;


public class MetadataController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public MetadataController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("genres")]
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres()
    {
        return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosAsync());
    }

    [HttpGet("people")]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople()
    {
        return Ok(await _unitOfWork.PersonRepository.GetAllPeople());
    }
}
