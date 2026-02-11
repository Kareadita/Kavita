using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[Authorize]
public class ColorScapeController(IUnitOfWork unitOfWork) : BaseApiController
{
    /// <summary>
    /// Returns the color scape for a series
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("series")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForSeries(int id)
    {
        var entity = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(id, UserId);
        return GetColorSpaceDto(entity);
    }

    /// <summary>
    /// Returns the color scape for a volume
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("volume")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForVolume(int id)
    {
        var entity = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(id, UserId);
        return GetColorSpaceDto(entity);
    }

    /// <summary>
    /// Returns the color scape for a chapter
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("chapter")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForChapter(int id)
    {
        var entity = await unitOfWork.ChapterRepository.GetChapterDtoAsync(id, UserId);
        return GetColorSpaceDto(entity);
    }


    private ActionResult<ColorScapeDto> GetColorSpaceDto(IHasCoverImage entity)
    {
        if (entity == null) return Ok(ColorScapeDto.Empty);
        return Ok(new ColorScapeDto(entity.PrimaryColor, entity.SecondaryColor));
    }
}
