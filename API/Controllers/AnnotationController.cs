#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Metadata.Browse.Requests;
using API.DTOs.Reader;
using API.Extensions;
using API.Helpers;
using API.Middleware;
using API.Services;
using API.SignalR;
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

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(UserId, filter, userParams);
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
        return Ok(await unitOfWork.UserRepository.GetAnnotations(UserId, chapterId));
    }

    /// <summary>
    /// Returns all annotations by Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("all-for-series")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotationsBySeries(int seriesId)
    {
        return Ok(await unitOfWork.UserRepository.GetAnnotationDtosBySeries(UserId, seriesId));
    }

    /// <summary>
    /// Returns the Annotation by Id. User must have access to annotation.
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpGet("{annotationId}")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotation(int annotationId)
    {
        return Ok(await unitOfWork.UserRepository.GetAnnotationDtoById(UserId, annotationId));
    }

    /// <summary>
    /// Create a new Annotation for the user against a Chapter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation(AnnotationDto dto)
    {
        try
        {
            return Ok(await annotationService.CreateAnnotation(UserId, dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }
    }

    /// <summary>
    /// Update the modifiable fields (Spoiler, highlight slot, and comment) for an annotation
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<AnnotationDto>> UpdateAnnotation(AnnotationDto dto)
    {
        try
        {
            return Ok(await annotationService.UpdateAnnotation(UserId, dto));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }
    }

    /// <summary>
    /// Adds a like for the currently authenticated user if not already from the annotations with given ids
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpPost("like")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> LikeAnnotations(IList<int> ids)
    {
        var userId = UserId;

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
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UnLikeAnnotations(IList<int> ids)
    {
        var userId = UserId;

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
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteAnnotation(int annotationId)
    {
        var annotation = await unitOfWork.AnnotationRepository.GetAnnotation(annotationId);
        if (annotation == null || annotation.AppUserId != UserId) return BadRequest(await localizationService.Translate(UserId, "annotation-delete"));

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
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteAnnotationsBulk(IList<int> annotationIds)
    {
        var userId = UserId;

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

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(UserId, filter, userParams);
        var annotations = list.Select(a => a.Id).ToList();

        var json = await annotationService.ExportAnnotations(UserId, annotations);
        if (string.IsNullOrEmpty(json)) return BadRequest();

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = System.Web.HttpUtility.UrlEncode($"annotations_export_{UserId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_filtered");
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
        var json = await annotationService.ExportAnnotations(UserId, annotations);
        if (string.IsNullOrEmpty(json)) return BadRequest();

        var bytes = Encoding.UTF8.GetBytes(json);

        var fileName = System.Web.HttpUtility.UrlEncode($"annotations_export_{UserId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
        if (annotations != null)
        {
            fileName += "_user_selection";
        }

        return File(bytes, "application/json", fileName + ".json");
    }
}
