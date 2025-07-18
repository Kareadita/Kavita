using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class AnnotationController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public AnnotationController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Returns the annotations for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<AnnotationDto>>> GetAnnotations(int chapterId)
    {

        return Ok(await _unitOfWork.UserRepository.GetAnnotations(User.GetUserId(), chapterId));
    }

    [HttpPost("create")]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation(CreateAnnotationRequest dto)
    {
        try
        {
            if (dto.HighlightCount == 0 || string.IsNullOrWhiteSpace(dto.SelectedText))
            {
                return BadRequest("Invalid Payload");
            }

            var annotation = new AppUserAnnotation()
            {
                XPath = dto.XPath,
                EndingXPath = dto.EndingXPath,
                ChapterId = dto.ChapterId,
                SeriesId = dto.SeriesId,
                VolumeId = dto.VolumeId,
                HighlightCount = dto.HighlightCount,
                SelectedText = dto.SelectedText,
                Comment = dto.Comment,
                ContainsSpoiler = dto.ContainsSpoiler,
                PageNumber = dto.PageNumber,
                HighlightColor = dto.HighlightColor,
                AppUserId = User.GetUserId()
            };

            _unitOfWork.AnnotationRepository.Attach(annotation);
            await _unitOfWork.CommitAsync();

            return Ok(await _unitOfWork.AnnotationRepository.GetAnnotationDto(annotation.Id));
        }
        catch (Exception ex)
        {
            return BadRequest("Failed to create annotation, try again");
        }
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteAnnotation(int annotationId)
    {
        var annotation = await _unitOfWork.AnnotationRepository.GetAnnotation(annotationId);
        if (annotation == null || annotation.AppUserId != User.GetUserId()) return BadRequest("Cannot  delete annotation");

        _unitOfWork.AnnotationRepository.Remove(annotation);
        await _unitOfWork.CommitAsync();
        return Ok();
    }
}
