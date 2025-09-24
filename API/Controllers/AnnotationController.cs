#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Metadata.Browse.Requests;
using API.DTOs.Reader;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.SignalR;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class AnnotationController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnnotationController> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly IEventHub _eventHub;
    private readonly IAnnotationService _annotationService;

    public AnnotationController(IUnitOfWork unitOfWork, ILogger<AnnotationController> logger,
        ILocalizationService localizationService, IEventHub eventHub, IAnnotationService annotationService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _localizationService = localizationService;
        _eventHub = eventHub;
        _annotationService = annotationService;
    }

    /// <summary>
    /// Returns a list of annotations for browsing
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("all-filtered")]
    public async Task<ActionResult<PagedList<AnnotationDto>>> GetAnnotationsForBrowse(BrowseAnnotationFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var list = await _unitOfWork.AnnotationRepository.GetAnnotationDtos(User.GetUserId(), filter, userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);

        return Ok(list);
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

    /// <summary>
    /// Returns all annotations by Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("all-for-series")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotationsBySeries(int seriesId)
    {
        return Ok(await _unitOfWork.UserRepository.GetAnnotationDtosBySeries(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Returns the Annotation by Id. User must have access to annotation.
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpGet("{annotationId}")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotation(int annotationId)
    {
        return Ok(await _unitOfWork.UserRepository.GetAnnotationDtoById(User.GetUserId(), annotationId));
    }

    /// <summary>
    /// Create a new Annotation for the user against a Chapter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation(AnnotationDto dto)
    {
        try
        {
            return Ok(_annotationService.CreateAnnotation(User.GetUserId(), dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
    }

    /// <summary>
    /// Update the modifiable fields (Spoiler, highlight slot, and comment) for an annotation
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult<AnnotationDto>> UpdateAnnotation(AnnotationDto dto)
    {
        try
        {
            return Ok(await _annotationService.UpdateAnnotation(User.GetUserId(), dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
    }

    private string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        try
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            return document.DocumentNode.InnerText.Replace("&nbsp;", " ");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Invalid html, cannot parse plain text");
            return string.Empty;
        }
    }

    /// <summary>
    /// Delete the annotation for the user
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteAnnotation(int annotationId)
    {
        var annotation = await _unitOfWork.AnnotationRepository.GetAnnotation(annotationId);
        if (annotation == null || annotation.AppUserId != User.GetUserId()) return BadRequest(_localizationService.Translate(User.GetUserId(), "annotation-delete"));

        _unitOfWork.AnnotationRepository.Remove(annotation);
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Exports Annotations for the User
    /// </summary>
    /// <returns></returns>
    [HttpGet("export")]
    public async Task<IActionResult> ExportAnnotations()
    {
        var exportFile = await _annotationService.ExportAnnotations(User.GetUserId());
        if (string.IsNullOrEmpty(exportFile)) return BadRequest();


        return PhysicalFile(exportFile, "application/json", true);
    }
}
