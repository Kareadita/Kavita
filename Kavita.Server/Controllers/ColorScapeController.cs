using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Models.DTOs.Theme;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[Authorize]
public class ColorScapeController(IUnitOfWork unitOfWork) : BaseApiController
{
    /// <summary>
    /// Returns the color scape for a series
    /// </summary>
    [SeriesAccess]
    [HttpGet("series")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForSeries(int seriesId)
    {
        return Ok(await unitOfWork.SeriesRepository.GetColorScapeAsync(seriesId, HttpContext.RequestAborted) ?? ColorScapeDto.Empty);
    }

    /// <summary>
    /// Returns the color scape for a volume
    /// </summary>
    [VolumeAccess]
    [HttpGet("volume")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForVolume(int volumeId)
    {
        return Ok(await unitOfWork.VolumeRepository.GetColorScapeAsync(volumeId, HttpContext.RequestAborted) ?? ColorScapeDto.Empty);
    }

    /// <summary>
    /// Returns the color scape for a chapter
    /// </summary>
    [ChapterAccess]
    [HttpGet("chapter")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForChapter(int chapterId)
    {
        return Ok(await unitOfWork.ChapterRepository.GetColorScapeAsync(chapterId, HttpContext.RequestAborted) ?? ColorScapeDto.Empty);
    }

    /// <summary>
    /// Returns the color scape for a person
    /// </summary>
    [PersonAccess]
    [HttpGet("person")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForPerson(int personId)
    {
        return Ok(await unitOfWork.PersonRepository.GetColorScapeAsync(personId, HttpContext.RequestAborted) ?? ColorScapeDto.Empty);
    }

    /// <summary>
    /// Returns the color scape for a reading list
    /// </summary>
    [ReadingListAccess]
    [HttpGet("reading-list")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForReadingList(int readingListId)
    {
        return Ok(await unitOfWork.ReadingListRepository.GetColorScapeAsync(readingListId, HttpContext.RequestAborted) ?? ColorScapeDto.Empty);
    }
}
