using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Fetches genres from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all genres</param>
    /// <returns></returns>
    [HttpGet("genres")]
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosForLibrariesAsync(ids));
        }

        return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosAsync());
    }

    /// <summary>
    /// Fetches people from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all people</param>
    /// <returns></returns>
    [HttpGet("people")]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(ids));
        }
        return Ok(await _unitOfWork.PersonRepository.GetAllPeople());
    }
}
