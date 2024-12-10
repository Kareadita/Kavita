using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Collection;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

/// <summary>
/// APIs for Collections
/// </summary>
public class CollectionController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICollectionTagService _collectionService;
    private readonly ILocalizationService _localizationService;
    private readonly IExternalMetadataService _externalMetadataService;
    private readonly ISmartCollectionSyncService _collectionSyncService;
    private readonly ILogger<CollectionController> _logger;
    private readonly IEventHub _eventHub;

    /// <inheritdoc />
    public CollectionController(IUnitOfWork unitOfWork, ICollectionTagService collectionService,
        ILocalizationService localizationService, IExternalMetadataService externalMetadataService,
        ISmartCollectionSyncService collectionSyncService, ILogger<CollectionController> logger,
        IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _collectionService = collectionService;
        _localizationService = localizationService;
        _externalMetadataService = externalMetadataService;
        _collectionSyncService = collectionSyncService;
        _logger = logger;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Returns all Collection tags for a given User
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUserCollectionDto>>> GetAllTags(bool ownedOnly = false)
    {
        return Ok(await _unitOfWork.CollectionTagRepository.GetCollectionDtosAsync(User.GetUserId(), !ownedOnly));
    }

    /// <summary>
    /// Returns a single Collection tag by Id for a given user
    /// </summary>
    /// <param name="collectionId"></param>
    /// <returns></returns>
    [HttpGet("single")]
    public async Task<ActionResult<IEnumerable<AppUserCollectionDto>>> GetTag(int collectionId)
    {
        var collections = await _unitOfWork.CollectionTagRepository.GetCollectionDtosAsync(User.GetUserId(), false);
        return Ok(collections.FirstOrDefault(c => c.Id == collectionId));
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
        return Ok(await _unitOfWork.CollectionTagRepository.GetCollectionDtosBySeriesAsync(User.GetUserId(), seriesId, !ownedOnly));
    }


    /// <summary>
    /// Checks if a collection exists with the name
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> DoesNameExists(string name)
    {
        return Ok(await _unitOfWork.CollectionTagRepository.CollectionExists(name, User.GetUserId()));
    }

    /// <summary>
    /// Updates an existing tag with a new title, promotion status, and summary.
    /// <remarks>UI does not contain controls to update title</remarks>
    /// </summary>
    /// <param name="updatedTag"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult> UpdateTag(AppUserCollectionDto updatedTag)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        try
        {
            if (await _collectionService.UpdateTag(updatedTag, User.GetUserId()))
            {
                await _eventHub.SendMessageAsync(MessageFactory.CollectionUpdated,
                    MessageFactory.CollectionUpdatedEvent(updatedTag.Id), false);
                return Ok(await _localizationService.Translate(User.GetUserId(), "collection-updated-successfully"));
            }
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    /// <summary>
    /// Promote/UnPromote multiple collections in one go. Will only update the authenticated user's collections and will only work if the user has promotion role
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("promote-multiple")]
    public async Task<ActionResult> PromoteMultipleCollections(PromoteCollectionsDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        // This needs to take into account owner as I can select other users cards
        var collections = await _unitOfWork.CollectionTagRepository.GetCollectionsByIds(dto.CollectionIds);
        var userId = User.GetUserId();

        if (!User.IsInRole(PolicyConstants.PromoteRole) && !User.IsInRole(PolicyConstants.AdminRole))
        {
            return BadRequest(await _localizationService.Translate(userId, "permission-denied"));
        }

        foreach (var collection in collections)
        {
            if (collection.AppUserId != userId) continue;
            collection.Promoted = dto.Promoted;
            _unitOfWork.CollectionTagRepository.Update(collection);
        }

        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }


    /// <summary>
    /// Delete multiple collections in one go
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-multiple")]
    public async Task<ActionResult> DeleteMultipleCollections(DeleteCollectionsDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        // This needs to take into account owner as I can select other users cards
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Collections);
        if (user == null) return Unauthorized();
        user.Collections = user.Collections.Where(uc => !dto.CollectionIds.Contains(uc.Id)).ToList();
        _unitOfWork.UserRepository.Update(user);


        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Adds multiple series to a collection. If tag id is 0, this will create a new tag.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-for-series")]
    public async Task<ActionResult> AddToMultipleSeries(CollectionTagBulkAddDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        // Create a new tag and save
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Collections);
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
            return BadRequest(_localizationService.Translate(User.GetUserId(), "collection-doesnt-exists"));
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(dto.SeriesIds.ToList(), false);
        foreach (var s in series)
        {
            if (tag.Items.Contains(s)) continue;
            tag.Items.Add(s);
        }
        _unitOfWork.UserRepository.Update(user);
        if (await _unitOfWork.CommitAsync()) return Ok();

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    /// <summary>
    /// For a given tag, update the summary if summary has changed and remove a set of series from the tag.
    /// </summary>
    /// <param name="updateSeriesForTagDto"></param>
    /// <returns></returns>
    [HttpPost("update-series")]
    public async Task<ActionResult> RemoveTagFromMultipleSeries(UpdateSeriesForTagDto updateSeriesForTagDto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(updateSeriesForTagDto.Tag.Id, CollectionIncludes.Series);
            if (tag == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "collection-doesnt-exist"));

            if (await _collectionService.RemoveTagFromSeries(tag, updateSeriesForTagDto.SeriesIdsToRemove))
                return Ok(await _localizationService.Translate(User.GetUserId(), "collection-updated"));
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    /// <summary>
    /// Removes the collection tag from the user
    /// </summary>
    /// <param name="tagId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteTag(int tagId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        try
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Collections);
            if (user == null) return Unauthorized();
            if (user.Collections.All(c => c.Id != tagId))
                return BadRequest(await _localizationService.Translate(user.Id, "access-denied"));

            if (await _collectionService.DeleteTag(tagId, user))
            {
                return Ok(await _localizationService.Translate(User.GetUserId(), "collection-deleted"));
            }
        }
        catch (Exception ex)
        {

            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    /// <summary>
    /// For the authenticated user, if they have an active Kavita+ subscription and a MAL username on record,
    /// fetch their Mal interest stacks (including restacks)
    /// </summary>
    /// <returns></returns>
    [HttpGet("mal-stacks")]
    public async Task<ActionResult<IList<MalStackDto>>> GetMalStacksForUser()
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        return Ok(await _externalMetadataService.GetStacksForUser(User.GetUserId()));
    }

    /// <summary>
    /// Imports a MAL Stack into Kavita
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("import-stack")]
    public async Task<ActionResult> ImportMalStack(MalStackDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Collections);
        if (user == null) return Unauthorized();
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));


        // Validation check to ensure stack doesn't exist already
        if (await _unitOfWork.CollectionTagRepository.CollectionExists(dto.Title, user.Id))
        {
            return BadRequest(_localizationService.Translate(user.Id, "collection-already-exists"));
        }

        try
        {
            // Create new collection
            var newCollection = new AppUserCollectionBuilder(dto.Title)
                .WithSource(ScrobbleProvider.Mal)
                .WithSourceUrl(dto.Url)
                .Build();
            user.Collections.Add(newCollection);

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();

            // Trigger Stack Refresh for just one stack (not all)
            BackgroundJob.Enqueue(() => _collectionSyncService.Sync(newCollection.Id));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue importing MAL Stack");
        }

        return BadRequest(_localizationService.Translate(user.Id, "error-import-stack"));
    }
}
