using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Collection;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
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
    private readonly ILogger<CollectionController> _logger;

    /// <inheritdoc />
    public CollectionController(IUnitOfWork unitOfWork, ICollectionTagService collectionService,
        ILocalizationService localizationService, IExternalMetadataService externalMetadataService,
        ILogger<CollectionController> logger)
    {
        _unitOfWork = unitOfWork;
        _collectionService = collectionService;
        _localizationService = localizationService;
        _externalMetadataService = externalMetadataService;
        _logger = logger;
    }

    /// <summary>
    /// Returns all Collection tags for a given User
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUserCollectionDto>>> GetAllTags(bool ownedOnly = false)
    {
        return Ok(await _unitOfWork.CollectionTagRepository.GetTagsAsync(User.GetUserId(), !ownedOnly));
    }


    /// <summary>
    /// Checks if a collection exists with the name
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> DoesNameExists(string name)
    {
        return Ok(await _unitOfWork.CollectionTagRepository.TagExists(name, User.GetUserId()));
    }

    /// <summary>
    /// Updates an existing tag with a new title, promotion status, and summary.
    /// <remarks>UI does not contain controls to update title</remarks>
    /// </summary>
    /// <param name="updatedTag"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateTag(AppUserCollectionDto updatedTag)
    {
        try
        {
            if (await _collectionService.UpdateTag(updatedTag, User.GetUserId()))
            {
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
    /// Adds multiple series to a collection. If tag id is 0, this will create a new tag.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update-for-series")]
    public async Task<ActionResult> AddToMultipleSeries(CollectionTagBulkAddDto dto)
    {
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

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(dto.SeriesIds.ToList());
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
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update-series")]
    public async Task<ActionResult> RemoveTagFromMultipleSeries(UpdateSeriesForTagDto updateSeriesForTagDto)
    {
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
        try
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Collections);
            if (user == null || user.Collections.All(c => c.Id != tagId)) return Unauthorized();

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
        return Ok(await _externalMetadataService.GetStacksForUser(User.GetUserId()));
    }
}
