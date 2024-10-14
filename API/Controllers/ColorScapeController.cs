using System.Threading.Tasks;
using API.Data;
using API.DTOs.Theme;
using API.Entities.Interfaces;
using API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class ColorScapeController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public ColorScapeController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Returns the color scape for a series
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("series")]
    public async Task<ActionResult<ColorScapeDto>> GetColorScapeForSeries(int id)
    {
        var entity = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(id, User.GetUserId());
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
        var entity = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(id, User.GetUserId());
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
        var entity = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(id);
        return GetColorSpaceDto(entity);
    }


    private ActionResult<ColorScapeDto> GetColorSpaceDto(IHasCoverImage entity)
    {
        if (entity == null) return Ok(ColorScapeDto.Empty);
        return Ok(new ColorScapeDto(entity.PrimaryColor, entity.SecondaryColor));
    }
}
