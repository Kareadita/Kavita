using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Reader;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

public class AnnotationController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
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
    public async Task<ActionResult<PagedList<AnnotationDto>>> GetAnnotationsForBrowse(AnnotationFilterDto filter, [FromQuery] UserParams? userParams)
    {
        var ct = HttpContext.RequestAborted;
        userParams ??= UserParams.Default;

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(UserId, filter, userParams, ct);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);

        return Ok(list);
    }

    /// <summary>
    /// Returns the annotations for the given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<AnnotationDto>>> GetAnnotations(int chapterId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.UserRepository.GetAnnotations(UserId, chapterId, ct));
    }

    /// <summary>
    /// Returns all annotations by Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("all-for-series")]
    public async Task<ActionResult<AnnotationDto>> GetAnnotationsBySeries(int seriesId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.UserRepository.GetAnnotationDtosBySeries(UserId, seriesId, ct));
    }

    /// <summary>
    /// Returns the Annotation by Id. User must have access to annotation.
    /// </summary>
    /// <param name="annotationId"></param>
    /// <returns></returns>
    [HttpGet("{annotationId}")]
    public async Task<ActionResult<AnnotationDto?>> GetAnnotation(int annotationId)
    {
        var ct = HttpContext.RequestAborted;
        var annotation = await unitOfWork.UserRepository.GetAnnotationDtoById(UserId, annotationId, ct);
        if (annotation == null) return NotFound();

        if (!await unitOfWork.UserRepository.HasAccessToChapter(UserId, annotation.ChapterId, ct))
        {
            return NotFound();
        }

        return Ok(annotation);
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
        var ct = HttpContext.RequestAborted;
        try
        {
            return Ok(await annotationService.CreateAnnotation(UserId, dto, ct));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
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
        var ct = HttpContext.RequestAborted;
        try
        {
            return Ok(await annotationService.UpdateAnnotation(UserId, dto, ct));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
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
        var ct = HttpContext.RequestAborted;
        var userId = UserId;

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, ids, ct);
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
            await unitOfWork.CommitAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        var userId = UserId;

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, ids, ct);
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
            await unitOfWork.CommitAsync(ct);
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
        var ct = HttpContext.RequestAborted;
        var annotation = await unitOfWork.AnnotationRepository.GetAnnotation(annotationId, ct);
        if (annotation == null || annotation.AppUserId != UserId) return BadRequest(await localizationService.TranslateAsync(UserId, "annotation-delete"));

        unitOfWork.AnnotationRepository.Remove(annotation);
        await unitOfWork.CommitAsync(ct);

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
        var ct = HttpContext.RequestAborted;
        var userId = UserId;

        var annotations = await unitOfWork.AnnotationRepository.GetAnnotations(userId, annotationIds, ct);
        if (annotations.Any(a => a.AppUserId != userId))
        {
            return BadRequest();
        }

        unitOfWork.AnnotationRepository.Remove(annotations);
        await unitOfWork.CommitAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Exports annotations for the given users
    /// </summary>
    /// <returns></returns>
    [HttpPost("export-filter")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<IActionResult> ExportAnnotationsFilter(AnnotationFilterDto filter, [FromQuery] UserParams? userParams)
    {
        var ct = HttpContext.RequestAborted;
        userParams ??= UserParams.Default;

        var list = await unitOfWork.AnnotationRepository.GetAnnotationDtos(UserId, filter, userParams, ct);
        var annotations = list.Select(a => a.Id).ToList();

        var json = await annotationService.ExportAnnotations(UserId, annotations, ct);
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
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<IActionResult> ExportAnnotations(IList<int>? annotations = null)
    {
        var ct = HttpContext.RequestAborted;
        var json = await annotationService.ExportAnnotations(UserId, annotations, ct);
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
