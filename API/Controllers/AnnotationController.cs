using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class AnnotationController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnnotationController> _logger;
    private readonly IBookService _bookService;
    private readonly ILocalizationService _localizationService;
    private readonly IEventHub _eventHub;

    public AnnotationController(IUnitOfWork unitOfWork, ILogger<AnnotationController> logger,
        IBookService bookService, ILocalizationService localizationService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _bookService = bookService;
        _localizationService = localizationService;
        _eventHub = eventHub;
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
            if (dto.HighlightCount == 0 || string.IsNullOrWhiteSpace(dto.SelectedText))
            {
                return BadRequest(_localizationService.Translate(User.GetUserId(), "invalid-payload"));
            }

            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId);
            if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

            var chapterTitle = string.Empty;
            try
            {
                var toc = await _bookService.GenerateTableOfContents(chapter);
                var pageTocs = BookChapterItemHelper.GetTocForPage(toc, dto.PageNumber);
                if (pageTocs.Count > 0)
                {
                    chapterTitle = pageTocs[0].Title;
                }
            }
            catch (KavitaException)
            {
                /* Swallow */
            }

            var annotation = new AppUserAnnotation()
            {
                XPath = dto.XPath,
                EndingXPath = dto.EndingXPath,
                ChapterId = dto.ChapterId,
                SeriesId = dto.SeriesId,
                VolumeId = dto.VolumeId,
                LibraryId = dto.LibraryId,
                HighlightCount = dto.HighlightCount,
                SelectedText = dto.SelectedText,
                Comment = dto.Comment,
                ContainsSpoiler = dto.ContainsSpoiler,
                PageNumber = dto.PageNumber,
                SelectedSlotIndex = dto.SelectedSlotIndex,
                AppUserId = User.GetUserId(),
                Context = dto.Context,
                ChapterTitle = chapterTitle
            };

            _unitOfWork.AnnotationRepository.Attach(annotation);
            await _unitOfWork.CommitAsync();

            return Ok(await _unitOfWork.AnnotationRepository.GetAnnotationDto(annotation.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when creating an annotation on {ChapterId} - Page {Page}", dto.ChapterId, dto.PageNumber);
            return BadRequest(_localizationService.Translate(User.GetUserId(), "annotation-failed-create"));
        }
    }

    /// <summary>
    /// Update the modifable fields (Spoiler, highlight slot, and comment) for an annotation
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult<AnnotationDto>> UpdateAnnotation(AnnotationDto dto)
    {
        try
        {
            var annotation = await _unitOfWork.AnnotationRepository.GetAnnotation(dto.Id);
            if (annotation == null || annotation.AppUserId != User.GetUserId()) return BadRequest();

            annotation.ContainsSpoiler = dto.ContainsSpoiler;
            annotation.SelectedSlotIndex = dto.SelectedSlotIndex;
            annotation.Comment = dto.Comment;
            _unitOfWork.AnnotationRepository.Update(annotation);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                await _eventHub.SendMessageToAsync(MessageFactory.AnnotationUpdate, MessageFactory.AnnotationUpdateEvent(dto),
                    User.GetUserId());
                return Ok(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception updating Annotation for Chapter {ChapterId} - Page {PageNumber}",  dto.ChapterId, dto.PageNumber);
            return BadRequest();
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
        var annotation = await _unitOfWork.AnnotationRepository.GetAnnotation(annotationId);
        if (annotation == null || annotation.AppUserId != User.GetUserId()) return BadRequest(_localizationService.Translate(User.GetUserId(), "annotation-delete"));

        _unitOfWork.AnnotationRepository.Remove(annotation);
        await _unitOfWork.CommitAsync();
        return Ok();
    }
}
