using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Server.Attributes;
using Kavita.Server.Helpers;
using Kavita.Services.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nager.ArticleNumber;

namespace Kavita.Server.Controllers;

public class ChapterController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    IEventHub eventHub,
    ILogger<ChapterController> logger)
    : BaseApiController
{

    /// <summary>
    /// Gets a single chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet]
    [ChapterAccess]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, UserId);

        return Ok(chapter);
    }

    /// <summary>
    /// Removes a Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpDelete]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> DeleteChapter(int chapterId)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId,
            ChapterIncludes.Files | ChapterIncludes.ExternalReviews | ChapterIncludes.ExternalRatings);
        if (chapter == null)
            return BadRequest(localizationService.Translate(UserId, "chapter-doesnt-exist"));

        var vol = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId, VolumeIncludes.Chapters);
        if (vol == null) return BadRequest(localizationService.Translate(UserId, "volume-doesnt-exist"));

        // If there is only 1 chapter within the volume, then we need to remove the volume
        var needToRemoveVolume = vol.Chapters.Count == 1;
        if (needToRemoveVolume)
        {
            unitOfWork.VolumeRepository.Remove(vol);
        }
        else
        {
            unitOfWork.ChapterRepository.Remove(chapter);
        }

        // If we removed the volume, do an additional check if we need to delete the actual series as well or not
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(vol.SeriesId, SeriesIncludes.ExternalData | SeriesIncludes.Volumes);
        var needToRemoveSeries = needToRemoveVolume && series != null && series.Volumes.Count <= 1;
        if (needToRemoveSeries)
        {
            unitOfWork.SeriesRepository.Remove(series!);
        }



        if (!await unitOfWork.CommitAsync()) return Ok(false);

        await eventHub.SendMessageAsync(MessageFactory.ChapterRemoved, MessageFactory.ChapterRemovedEvent(chapter.Id, vol.SeriesId), false);
        if (needToRemoveVolume)
        {
            await eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(chapter.VolumeId, vol.SeriesId), false);
        }

        if (needToRemoveSeries)
        {
            await eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
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
    [HttpPost("delete-multiple")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
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
            var chapters = (await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds)).ToList();

            // Group chapters by their volume
            var volumesToUpdate = chapters.GroupBy(c => c.VolumeId).ToList();
            var removedVolumes = new List<int>();

            foreach (var volumeGroup in volumesToUpdate)
            {
                var volumeId = volumeGroup.Key;
                var chaptersToDelete = volumeGroup.ToList();

                // Fetch the volume
                var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId, VolumeIncludes.Chapters);
                if (volume == null)
                    return BadRequest(localizationService.Translate(UserId, "volume-doesnt-exist"));

                // Check if all chapters in the volume are being deleted
                var isVolumeToBeRemoved = volume.Chapters.Count == chaptersToDelete.Count;

                if (isVolumeToBeRemoved)
                {
                    unitOfWork.VolumeRepository.Remove(volume);
                    removedVolumes.Add(volume.Id);
                }
                else
                {
                    // Remove only the specified chapters
                    unitOfWork.ChapterRepository.Remove(chaptersToDelete);
                }
            }

            if (!await unitOfWork.CommitAsync()) return Ok(false);

            // Send events for removed chapters
            foreach (var chapter in chapters)
            {
                await eventHub.SendMessageAsync(MessageFactory.ChapterRemoved,
                    MessageFactory.ChapterRemovedEvent(chapter.Id, seriesId), false);
            }

            // Send events for removed volumes
            foreach (var volumeId in removedVolumes)
            {
                await eventHub.SendMessageAsync(MessageFactory.VolumeRemoved,
                    MessageFactory.VolumeRemovedEvent(volumeId, seriesId), false);
            }

            return Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured while deleting chapters");
            return BadRequest(localizationService.Translate(UserId, "generic-error"));
        }

    }


    /// <summary>
    /// Update chapter metadata
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateChapterMetadata(UpdateChapterDto dto)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(dto.Id,
            ChapterIncludes.People | ChapterIncludes.Genres | ChapterIncludes.Tags, HttpContext.RequestAborted);
        if (chapter == null)
            return BadRequest(localizationService.Translate(UserId, "chapter-doesnt-exist"));

        var seriesId = await unitOfWork.ChapterRepository.GetSeriesIdForChapter(chapter.Id, HttpContext.RequestAborted);

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

        ExternalMetadataIdHelper.SetExternalMetadataIds(chapter, dto);


        #region Genres
        chapter.Genres ??= [];
        await TagHelper.UpdateEntityTags(chapter.Genres, dto.Genres.Select(t => t.Title), unitOfWork.DataContext.Genre, unitOfWork);
        #endregion

        #region Tags
        chapter.Tags ??= [];
        await TagHelper.UpdateEntityTags(chapter.Tags, dto.Tags.Select(t => t.Title), unitOfWork.DataContext.Tag, unitOfWork);
        #endregion

        #region People
        chapter.People ??= [];

        // Update writers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Writers.Select(p => p.Name).ToList(),
            PersonRole.Writer,
            unitOfWork
        );

        // Update characters
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Characters.Select(p => p.Name).ToList(),
            PersonRole.Character,
            unitOfWork
        );

        // Update pencillers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Pencillers.Select(p => p.Name).ToList(),
            PersonRole.Penciller,
            unitOfWork
        );

        // Update inkers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Inkers.Select(p => p.Name).ToList(),
            PersonRole.Inker,
            unitOfWork
        );

        // Update colorists
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Colorists.Select(p => p.Name).ToList(),
            PersonRole.Colorist,
            unitOfWork
        );

        // Update letterers
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Letterers.Select(p => p.Name).ToList(),
            PersonRole.Letterer,
            unitOfWork
        );

        // Update cover artists
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.CoverArtists.Select(p => p.Name).ToList(),
            PersonRole.CoverArtist,
            unitOfWork
        );

        // Update editors
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Editors.Select(p => p.Name).ToList(),
            PersonRole.Editor,
            unitOfWork
        );

        // Update publishers
        var updatedPublishers = await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Publishers.Select(p => p.Name).ToList(),
            PersonRole.Publisher,
            unitOfWork
        );

        if (updatedPublishers)
            chapter.KPlusOverrides.Remove(MetadataSettingField.ChapterPublisher);

        // Update translators
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Translators.Select(p => p.Name).ToList(),
            PersonRole.Translator,
            unitOfWork
        );

        // Update imprints
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Imprints.Select(p => p.Name).ToList(),
            PersonRole.Imprint,
            unitOfWork
        );

        // Update teams
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Teams.Select(p => p.Name).ToList(),
            PersonRole.Team,
            unitOfWork
        );

        // Update locations
        await PersonHelper.UpdateChapterPeopleAsync(
            chapter,
            dto.Locations.Select(p => p.Name).ToList(),
            PersonRole.Location,
            unitOfWork
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


        unitOfWork.ChapterRepository.Update(chapter);

        if (!unitOfWork.HasChanges())
        {
            return Ok();
        }

        if (seriesId.HasValue)
        {
            await eventHub.SendMessageAsync(MessageFactory.ChapterUpdated,
                MessageFactory.ChapterUpdatedEvent(chapter.Id, seriesId.Value),
                false, HttpContext.RequestAborted);
        }

        await unitOfWork.CommitAsync();

        return Ok();
    }


    /// <summary>
    /// Returns Ratings and Reviews for an individual Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter-detail-plus")]
    public async Task<ActionResult<ChapterDetailPlusDto>> ChapterDetailPlus([FromQuery] int chapterId)
    {
        var ret = new ChapterDetailPlusDto();

        var userReviews = (await unitOfWork.UserRepository.GetUserRatingDtosForChapterAsync(chapterId, UserId))
            .Where(r => !string.IsNullOrEmpty(r.Body))
            .OrderByDescending(review => review.Username.Equals(Username!) ? 1 : 0)
            .ToList();

        var ownRating = await unitOfWork.UserRepository.GetUserChapterRatingAsync(UserId, chapterId);
        if (ownRating != null)
        {
            ret.Rating = ownRating.Rating;
            ret.HasBeenRated = ownRating.HasBeenRated;
        }

        var externalReviews = await unitOfWork.ChapterRepository.GetExternalChapterReviewDtos(chapterId);
        if (externalReviews.Count > 0)
        {
            userReviews.AddRange(ReviewHelper.SelectSpectrumOfReviews(externalReviews));
        }

        ret.Reviews = userReviews;

        ret.Ratings = await unitOfWork.ChapterRepository.GetExternalChapterRatingDtos(chapterId);

        return Ok(ret);
    }

}
