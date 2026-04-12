using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.CollectionTags;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

/// <summary>
/// APIs for Collections
/// </summary>
/// <inheritdoc />
public class CollectionController(IUnitOfWork unitOfWork, ICollectionTagService collectionService,
    ILocalizationService localizationService, IExternalMetadataService externalMetadataService,
    ISmartCollectionSyncService collectionSyncService, ILogger<CollectionController> logger,
    IEventHub eventHub) : BaseApiController
{

    /// <summary>
    /// Returns all Collection tags for a given User
    /// </summary>
    /// <param name="ownedOnly">Exclude Promoted</param>
    /// <param name="sortByLastModified">Order by Last Modified rather than on Title</param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUserCollectionDto>>> GetAllTags(bool ownedOnly = false, bool sortByLastModified = false)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.CollectionTagRepository.GetCollectionDtosAsync(UserId, !ownedOnly, sortByLastModified, ct));
    }

    /// <summary>
    /// Returns a single Collection tag by Id for a given user
    /// </summary>
    /// <param name="collectionId"></param>
    /// <returns></returns>
    [HttpGet("single")]
    public async Task<ActionResult<AppUserCollectionDto>> GetTag(int collectionId)
    {
        var ct = HttpContext.RequestAborted;
        var result = await unitOfWork.CollectionTagRepository.GetCollectionDtoAsync(collectionId, UserId, ct);
        if (result == null) return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Returns all collections that contain the Series for the user with the option to allow for promoted collections (non-user owned)
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ownedOnly"></param>
    /// <returns></returns>
    [HttpGet("all-series")]
    public async Task<ActionResult<IEnumerable<AppUserCollectionDto>>> GetCollectionsBySeries(int seriesId, bool ownedOnly = false)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.CollectionTagRepository.GetCollectionDtosBySeriesAsync(UserId, seriesId, !ownedOnly, ct));
    }


    /// <summary>
    /// Checks if a collection exists with the name
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> DoesNameExists(string name)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.CollectionTagRepository.CollectionExists(name, UserId, ct));
    }

    /// <summary>
    /// Updates an existing tag with a new title, promotion status, and summary.
    /// <remarks>UI does not contain controls to update title</remarks>
    /// </summary>
    /// <param name="updatedTag"></param>
    /// <returns>The updated tag entity</returns>
    [HttpPost("update")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<AppUserCollectionDto>> UpdateTag(AppUserCollectionDto updatedTag)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            if (await collectionService.UpdateTag(updatedTag, UserId, ct))
            {
                await eventHub.SendMessageAsync(MessageFactory.CollectionUpdated,
                    MessageFactory.CollectionUpdatedEvent(updatedTag.Id), false, ct);
                return Ok(await unitOfWork.CollectionTagRepository.GetCollectionDtoAsync(updatedTag.Id, UserId, ct));
            }
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
    }

    /// <summary>
    /// Promote/UnPromote multiple collections in one go. Will only update the authenticated user's collections and will only work if the user has promotion role
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("promote-multiple")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> PromoteMultipleCollections(PromoteCollectionsDto dto)
    {
        var ct = HttpContext.RequestAborted;
        // This needs to take into account owner as I can select other users cards
        var collections = await unitOfWork.CollectionTagRepository.GetCollectionsByIds(dto.CollectionIds, ct: ct);
        var userId = UserId;

        if (!User.IsInRole(PolicyConstants.PromoteRole) && !User.IsInRole(PolicyConstants.AdminRole))
        {
            return BadRequest(await localizationService.TranslateAsync(userId, "permission-denied"));
        }

        foreach (var collection in collections)
        {
            if (collection.AppUserId != userId) continue;
            collection.Promoted = dto.Promoted;
            unitOfWork.CollectionTagRepository.Update(collection);
        }

        if (!unitOfWork.HasChanges()) return Ok();
        await unitOfWork.CommitAsync(ct);

        return Ok();
    }


    /// <summary>
    /// Delete multiple collections in one go
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-multiple")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteMultipleCollections(DeleteCollectionsDto dto)
    {
        var ct = HttpContext.RequestAborted;
        // This needs to take into account owner as I can select other users cards
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Collections, ct);
        if (user == null) return Unauthorized();

        user.Collections = user.Collections.Where(uc => !dto.CollectionIds.Contains(uc.Id)).ToList();
        unitOfWork.UserRepository.Update(user);


        if (!unitOfWork.HasChanges()) return Ok();
        await unitOfWork.CommitAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Adds multiple series to a collection. If tag id is 0, this will create a new tag.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-for-series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> AddToMultipleSeries(CollectionTagBulkAddDto dto)
    {
        var ct = HttpContext.RequestAborted;
        // Create a new tag and save
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Collections, ct);
        if (user == null) return Unauthorized();

        AppUserCollection? tag;
        if (dto.CollectionTagId == 0)
        {
            tag = new AppUserCollectionBuilder(dto.CollectionTagTitle).Build();
            user.Collections.Add(tag);
        }
        else
        {
            // Validate tag doesn't exist
            tag = user.Collections.FirstOrDefault(t => t.Id == dto.CollectionTagId);
        }

        if (tag == null)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "collection-doesnt-exists"));
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdsAsync(dto.SeriesIds.ToList(), false);
        foreach (var s in series)
        {
            if (tag.Items.Contains(s)) continue;
            tag.Items.Add(s);
        }
        unitOfWork.UserRepository.Update(user);
        if (await unitOfWork.CommitAsync(ct)) return Ok();

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
    }

    /// <summary>
    /// For a given tag, update the summary if summary has changed and remove a set of series from the tag.
    /// </summary>
    /// <param name="updateSeriesForTagDto"></param>
    /// <returns></returns>
    [HttpPost("update-series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> RemoveTagFromMultipleSeries(UpdateSeriesForTagDto updateSeriesForTagDto)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            var tag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(updateSeriesForTagDto.Tag.Id, CollectionIncludes.Series, ct);
            if (tag == null) return BadRequest(await localizationService.TranslateAsync(UserId, "collection-doesnt-exist"));

            if (await collectionService.RemoveTagFromSeries(tag, updateSeriesForTagDto.SeriesIdsToRemove, ct))
                return Ok(await localizationService.TranslateAsync(UserId, "collection-updated"));
        }
        catch (Exception)
        {
            await unitOfWork.RollbackAsync(ct);
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
    }

    /// <summary>
    /// Removes the collection tag from the user
    /// </summary>
    /// <param name="tagId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteTag(int tagId)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Collections, ct);
            if (user == null) return Unauthorized();

            if (user.Collections.All(c => c.Id != tagId))
                return BadRequest(await localizationService.TranslateAsync(user.Id, "access-denied"));

            if (await collectionService.DeleteTag(tagId, user, ct))
            {
                return Ok(await localizationService.TranslateAsync(UserId, "collection-deleted"));
            }
        }
        catch (Exception ex)
        {

            await unitOfWork.RollbackAsync(ct);
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
    }

    /// <summary>
    /// For the authenticated user, if they have an active Kavita+ subscription and a MAL username on record,
    /// fetch their Mal interest stacks (including restacks)
    /// </summary>
    /// <returns></returns>
    [HttpGet("mal-stacks")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<IList<MalStackDto>>> GetMalStacksForUser()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await externalMetadataService.GetStacksForUser(UserId, ct));
    }

    /// <summary>
    /// Imports a MAL Stack into Kavita
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("import-stack")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> ImportMalStack(MalStackDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.Collections, ct);
        if (user == null) return Unauthorized();

        // Validation check to ensure stack doesn't exist already
        if (await unitOfWork.CollectionTagRepository.CollectionExists(dto.Title, user.Id, ct))
        {
            return BadRequest(await localizationService.TranslateAsync(user.Id, "collection-already-exists"));
        }

        try
        {
            // Create new collection
            var newCollection = new AppUserCollectionBuilder(dto.Title)
                .WithSource(ScrobbleProvider.Mal)
                .WithSourceUrl(dto.Url)
                .Build();
            user.Collections.Add(newCollection);

            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync(ct);

            // Trigger Stack Refresh for just one stack (not all)
            BackgroundJob.Enqueue(() => collectionSyncService.Sync(newCollection.Id, CancellationToken.None));
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue importing MAL Stack");
        }

        return BadRequest(await localizationService.TranslateAsync(user.Id, "error-import-stack"));
    }
}
