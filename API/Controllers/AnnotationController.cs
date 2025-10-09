#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
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

public class AnnotationController(
    IUnitOfWork unitOfWork,
    ILogger<AnnotationController> logger,
    ILocalizationService localizationService,
    IEventHub eventHub,
    IAnnotationService annotationService)
    : BaseApiController
{

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

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(User.GetUserId(), filter, userParams);
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
        return Ok(await unitOfWork.UserRepository.GetAnnotations(User.GetUserId(), chapterId));
    }

    /// <summary>
    /// Returns all annotations by Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("all-for-series")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotationsBySeries(int seriesId)
    {
        return Ok(await unitOfWork.UserRepository.GetAnnotationDtosBySeries(User.GetUserId(), seriesId));
    }

    /// <summary>
    /// Returns the Annotation by Id. User must have access to annotation.
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpGet("{annotationId}")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotation(int annotationId)
    {
        return Ok(await unitOfWork.UserRepository.GetAnnotationDtoById(User.GetUserId(), annotationId));
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
            return Ok(await annotationService.CreateAnnotation(User.GetUserId(), dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(User.GetUserId(), ex.Message));
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
            return Ok(await annotationService.UpdateAnnotation(User.GetUserId(), dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(User.GetUserId(), ex.Message));
        }
    }

    /// <summary>
    /// Adds a like for the currently authenticated user if not already from the annotations with given ids
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpPost("like")]
    public async Task<ActionResult> LikeAnnotations(IList<int> ids)
    {
        var userId = User.GetUserId();

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, ids);
        if (annotations.Count != ids.Count)
        {
            return BadRequest();
        }

        foreach (var annotation in annotations.Where(a => !a.Likes.Contains(userId) && a.AppUserId != userId))
        {
            annotation.Likes.Add(userId);
            unitOfWork.AnnotationRepository.Update(annotation);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }


        return Ok();
    }

    /// <summary>
    /// Removes likes for the currently authenticated user if present from the annotations with given ids
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpPost("unlike")]
    public async Task<ActionResult> UnLikeAnnotations(IList<int> ids)
    {
        var userId = User.GetUserId();

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, ids);
        if (annotations.Count != ids.Count)
        {
            return BadRequest();
        }

        foreach (var annotation in annotations.Where(a => a.Likes.Contains(userId)))
        {
            annotation.Likes.Remove(userId);
            unitOfWork.AnnotationRepository.Update(annotation);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }


        return Ok();
    }

    /// <summary>
    /// Delete the annotation for the user
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteAnnotation(int annotationId)
    {
        var annotation = await unitOfWork.AnnotationRepository.GetAnnotation(annotationId);
        if (annotation == null || annotation.AppUserId != User.GetUserId()) return BadRequest(await localizationService.Translate(User.GetUserId(), "annotation-delete"));

        unitOfWork.AnnotationRepository.Remove(annotation);
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Removes annotations in bulk. Requires every annotation to be owned by the authenticated user
    /// </summary>
    /// <param name="annotationIds"></param>
    /// <returns></returns>
    [HttpPost("bulk-delete")]
    public async Task<ActionResult> DeleteAnnotationsBulk(IList<int> annotationIds)
    {
        var userId = User.GetUserId();

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, annotationIds);
        if (annotations.Any(a => a.AppUserId != userId))
        {
            return BadRequest();
        }

        unitOfWork.AnnotationRepository.Remove(annotations);
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Exports annotations for the given users
    /// </summary>
    /// <returns></returns>
    [HttpPost("export-filter")]
    public async Task<IActionResult> ExportAnnotationsFilter(BrowseAnnotationFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(User.GetUserId(), filter, userParams);
        var annotations = list.Select(a => a.Id).ToList();

        var json = await annotationService.ExportAnnotations(User.GetUserId(), annotations);
        if (string.IsNullOrEmpty(json)) return BadRequest();

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = System.Web.HttpUtility.UrlEncode($"annotations_export_{User.GetUserId()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_filtered");
        return File(bytes, "application/json", fileName + ".json");
    }

    /// <summary>
    /// Exports Annotations for the User
    /// </summary>
    /// <param name="annotations">Export annotations with the given ids</param>
    /// <returns></returns>
    [HttpPost("export")]
    public async Task<IActionResult> ExportAnnotations(IList<int>? annotations = null)
    {
        var json = await annotationService.ExportAnnotations(User.GetUserId(), annotations);
        if (string.IsNullOrEmpty(json)) return BadRequest();

        var bytes = Encoding.UTF8.GetBytes(json);

        var fileName = System.Web.HttpUtility.UrlEncode($"annotations_export_{User.GetUserId()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
        if (annotations != null)
        {
            fileName += "_user_selection";
        }

        return File(bytes, "application/json", fileName + ".json");
    }
}
