using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.MetadataMatching;
using API.Entities.Person;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nager.ArticleNumber;

namespace API.Controllers;

public class ChapterController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ChapterController> _logger;
    private readonly IMapper _mapper;

    public ChapterController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IEventHub eventHub, ILogger<ChapterController> logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _eventHub = eventHub;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a single chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter =
            await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId,
                ChapterIncludes.People | ChapterIncludes.Files);

        return Ok(chapter);
    }

    /// <summary>
    /// Removes a Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete]
    public async Task<ActionResult<bool>> DeleteChapter(int chapterId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId,
            ChapterIncludes.Files | ChapterIncludes.ExternalReviews | ChapterIncludes.ExternalRatings);
        if (chapter == null)
            return BadRequest(_localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        var vol = await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId, VolumeIncludes.Chapters);
        if (vol == null) return BadRequest(_localizationService.Translate(User.GetUserId(), "volume-doesnt-exist"));

        // If there is only 1 chapter within the volume, then we need to remove the volume
        var needToRemoveVolume = vol.Chapters.Count == 1;
        if (needToRemoveVolume)
        {
            _unitOfWork.VolumeRepository.Remove(vol);
        }
        else
        {
            _unitOfWork.ChapterRepository.Remove(chapter);
        }

        // If we removed the volume, do an additional check if we need to delete the actual series as well or not
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(vol.SeriesId, SeriesIncludes.ExternalData | SeriesIncludes.Volumes);
        var needToRemoveSeries = needToRemoveVolume && series != null && series.Volumes.Count <= 1;
        if (needToRemoveSeries)
        {
            _unitOfWork.SeriesRepository.Remove(series!);
        }



        if (!await _unitOfWork.CommitAsync()) return Ok(false);

        await _eventHub.SendMessageAsync(MessageFactory.ChapterRemoved, MessageFactory.ChapterRemovedEvent(chapter.Id, vol.SeriesId), false);
        if (needToRemoveVolume)
        {
            await _eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(chapter.VolumeId, vol.SeriesId), false);
        }

        if (needToRemoveSeries)
        {
            await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                MessageFactory.SeriesRemovedEvent(series!.Id, series.Name, series.LibraryId), false);
        }

        return Ok(true);
    }

    /// <summary>
    /// Deletes multiple chapters and any volumes with no leftover chapters
    /// </summary>
    /// <param name="seriesId">The ID of the series</param>
    /// <param name="dto">The IDs of the chapters to be deleted</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("delete-multiple")]
    public async Task<ActionResult<bool>> DeleteMultipleChapters([FromQuery] int seriesId, DeleteChaptersDto dto)
    {
        try
        {
            var chapterIds = dto.ChapterIds;
            if (chapterIds == null || chapterIds.Count == 0)
            {
                return BadRequest("ChapterIds required");
            }

            // Fetch all chapters to be deleted
            var chapters = (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds)).ToList();

            // Group chapters by their volume
            var volumesToUpdate = chapters.GroupBy(c => c.VolumeId).ToList();
            var removedVolumes = new List<int>();

            foreach (var volumeGroup in volumesToUpdate)
            {
                var volumeId = volumeGroup.Key;
                var chaptersToDelete = volumeGroup.ToList();

                // Fetch the volume
                var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId, VolumeIncludes.Chapters);
                if (volume == null)
                    return BadRequest(_localizationService.Translate(User.GetUserId(), "volume-doesnt-exist"));

                // Check if all chapters in the volume are being deleted
                var isVolumeToBeRemoved = volume.Chapters.Count == chaptersToDelete.Count;

                if (isVolumeToBeRemoved)
                {
                    _unitOfWork.VolumeRepository.Remove(volume);
                    removedVolumes.Add(volume.Id);
                }
                else
                {
                    // Remove only the specified chapters
                    _unitOfWork.ChapterRepository.Remove(chaptersToDelete);
                }
            }

            if (!await _unitOfWork.CommitAsync()) return Ok(false);

            // Send events for removed chapters
            foreach (var chapter in chapters)
            {
                await _eventHub.SendMessageAsync(MessageFactory.ChapterRemoved,
                    MessageFactory.ChapterRemovedEvent(chapter.Id, seriesId), false);
            }

            // Send events for removed volumes
            foreach (var volumeId in removedVolumes)
            {
                await _eventHub.SendMessageAsync(MessageFactory.VolumeRemoved,
                    MessageFactory.VolumeRemovedEvent(volumeId, seriesId), false);
            }

            return Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while deleting chapters");
            return BadRequest(_localizationService.Translate(User.GetUserId(), "generic-error"));
        }

    }


    /// <summary>
    /// Update chapter metadata
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateChapterMetadata(UpdateChapterDto dto)
    {
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(dto.Id,
            ChapterIncludes.People | ChapterIncludes.Genres | ChapterIncludes.Tags);
        if (chapter == null)
            return BadRequest(_localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        if (chapter.AgeRating != dto.AgeRating)
        {
            chapter.AgeRating = dto.AgeRating;
            chapter.KPlusOverrides.Remove(MetadataSettingField.AgeRating);
        }

        dto.Summary ??= string.Empty;

        if (chapter.Summary != dto.Summary.Trim())
        {
            chapter.Summary = dto.Summary.Trim();
            chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterSummary);
        }

        if (chapter.Language != dto.Language)
        {
            chapter.Language = dto.Language ?? string.Empty;
        }

        if (chapter.SortOrder.IsNot(dto.SortOrder))
        {
            chapter.SortOrder = dto.SortOrder; // TODO: Figure out validation
        }

        if (chapter.TitleName != dto.TitleName)
        {
            chapter.TitleName = dto.TitleName;
            chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterTitle);
        }

        if (chapter.ReleaseDate != dto.ReleaseDate)
        {
            chapter.ReleaseDate = dto.ReleaseDate;
            chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterReleaseDate);
        }

        if (!string.IsNullOrEmpty(dto.ISBN) && ArticleNumberHelper.IsValidIsbn10(dto.ISBN) ||
            ArticleNumberHelper.IsValidIsbn13(dto.ISBN))
        {
            chapter.ISBN = dto.ISBN;
        }

        if (string.IsNullOrEmpty(dto.WebLinks))
        {
            chapter.WebLinks = string.Empty;
        } else
        {
            chapter.WebLinks = string.Join(',', dto.WebLinks
                    .Split(',')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Trim())!
            );
        }


        #region Genres
        chapter.Genres ??= [];
        await GenreHelper.UpdateChapterGenres(chapter, dto.Genres.Select(t => t.Title), _unitOfWork);
        #endregion

        #region Tags
        chapter.Tags ??= [];
        await TagHelper.UpdateChapterTags(chapter, dto.Tags.Select(t => t.Title), _unitOfWork);
        #endregion

        #region People
        chapter.People ??= [];

        // Update writers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Writers.Select(p => p.Name).ToList(),
            PersonRole.Writer,
            _unitOfWork
        );

        // Update characters
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Characters.Select(p => p.Name).ToList(),
            PersonRole.Character,
            _unitOfWork
        );

        // Update pencillers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Pencillers.Select(p => p.Name).ToList(),
            PersonRole.Penciller,
            _unitOfWork
        );

        // Update inkers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Inkers.Select(p => p.Name).ToList(),
            PersonRole.Inker,
            _unitOfWork
        );

        // Update colorists
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Colorists.Select(p => p.Name).ToList(),
            PersonRole.Colorist,
            _unitOfWork
        );

        // Update letterers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Letterers.Select(p => p.Name).ToList(),
            PersonRole.Letterer,
            _unitOfWork
        );

        // Update cover artists
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.CoverArtists.Select(p => p.Name).ToList(),
            PersonRole.CoverArtist,
            _unitOfWork
        );

        // Update editors
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Editors.Select(p => p.Name).ToList(),
            PersonRole.Editor,
            _unitOfWork
        );

        // TODO: Only remove field if changes were made
        chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterPublisher);
        // Update publishers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Publishers.Select(p => p.Name).ToList(),
            PersonRole.Publisher,
            _unitOfWork
        );

        // Update translators
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Translators.Select(p => p.Name).ToList(),
            PersonRole.Translator,
            _unitOfWork
        );

        // Update imprints
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Imprints.Select(p => p.Name).ToList(),
            PersonRole.Imprint,
            _unitOfWork
        );

        // Update teams
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Teams.Select(p => p.Name).ToList(),
            PersonRole.Team,
            _unitOfWork
        );

        // Update locations
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Locations.Select(p => p.Name).ToList(),
            PersonRole.Location,
            _unitOfWork
        );
        #endregion

        #region Locks
        chapter.AgeRatingLocked = dto.AgeRatingLocked;
        chapter.LanguageLocked = dto.LanguageLocked;
        chapter.TitleNameLocked = dto.TitleNameLocked;
        chapter.SortOrderLocked = dto.SortOrderLocked;
        chapter.GenresLocked = dto.GenresLocked;
        chapter.TagsLocked = dto.TagsLocked;
        chapter.CharacterLocked = dto.CharacterLocked;
        chapter.ColoristLocked = dto.ColoristLocked;
        chapter.EditorLocked = dto.EditorLocked;
        chapter.InkerLocked = dto.InkerLocked;
        chapter.ImprintLocked = dto.ImprintLocked;
        chapter.LettererLocked = dto.LettererLocked;
        chapter.PencillerLocked = dto.PencillerLocked;
        chapter.PublisherLocked = dto.PublisherLocked;
        chapter.TranslatorLocked = dto.TranslatorLocked;
        chapter.CoverArtistLocked = dto.CoverArtistLocked;
        chapter.WriterLocked = dto.WriterLocked;
        chapter.SummaryLocked = dto.SummaryLocked;
        chapter.ISBNLocked = dto.ISBNLocked;
        chapter.ReleaseDateLocked = dto.ReleaseDateLocked;
        #endregion


        _unitOfWork.ChapterRepository.Update(chapter);

        if (!_unitOfWork.HasChanges())
        {
            return Ok();
        }

        // TODO: Emit a ChapterMetadataUpdate out

        await _unitOfWork.CommitAsync();


        return Ok();
    }

    /// <summary>
    /// Returns Ratings and Reviews for an individual Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-detail-plus")]
    public async Task<ActionResult<ChapterDetailPlusDto>> ChapterDetailPlus([FromQuery] int chapterId)
    {
        var ret = new ChapterDetailPlusDto();

        var userReviews = (await _unitOfWork.UserRepository.GetUserRatingDtosForChapterAsync(chapterId, User.GetUserId()))
            .Where(r => !string.IsNullOrEmpty(r.Body))
            .OrderByDescending(review => review.Username.Equals(User.GetUsername()) ? 1 : 0)
            .ToList();

        var ownRating = await _unitOfWork.UserRepository.GetUserChapterRatingAsync(User.GetUserId(), chapterId);
        if (ownRating != null)
        {
            ret.Rating = ownRating.Rating;
            ret.HasBeenRated = ownRating.HasBeenRated;
        }

        var externalReviews = await _unitOfWork.ChapterRepository.GetExternalChapterReviewDtos(chapterId);
        if (externalReviews.Count > 0)
        {
            userReviews.AddRange(ReviewHelper.SelectSpectrumOfReviews(externalReviews));
        }

        ret.Reviews = userReviews;

        ret.Ratings = await _unitOfWork.ChapterRepository.GetExternalChapterRatingDtos(chapterId);

        return Ok(ret);
    }

}
