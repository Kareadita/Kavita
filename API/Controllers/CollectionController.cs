using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.CollectionTags;
using API.Entities.Metadata;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <inheritdoc />
    public CollectionController(IUnitOfWork unitOfWork, ICollectionTagService collectionService,
        ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _collectionService = collectionService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Return a list of all collection tags on the server for the logged in user.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CollectionTagDto>>> GetAllTags()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        if (isAdmin)
        {
            return Ok(await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync());
        }

        return Ok(await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync(user.Id));
    }

    /// <summary>
    /// Searches against the collection tags on the DB and returns matches that meet the search criteria.
    /// <remarks>Search strings will be cleaned of certain fields, like %</remarks>
    /// </summary>
    /// <param name="queryString">Search term</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<CollectionTagDto>>> SearchTags(string queryString)
    {
        queryString ??= string.Empty;
        queryString = queryString.Replace(@"%", string.Empty);
        if (queryString.Length == 0) return await GetAllTags();

        return Ok(await _unitOfWork.CollectionTagRepository.SearchTagDtosAsync(queryString, User.GetUserId()));
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
        return Ok(await _collectionService.TagExistsByName(name));
    }

    /// <summary>
    /// Updates an existing tag with a new title, promotion status, and summary.
    /// <remarks>UI does not contain controls to update title</remarks>
    /// </summary>
    /// <param name="updatedTag"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateTag(CollectionTagDto updatedTag)
    {
        try
        {
            if (await _collectionService.UpdateTag(updatedTag)) return Ok(await _localizationService.Translate(User.GetUserId(), "collection-updated-successfully"));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }

    /// <summary>
    /// Adds a collection tag onto multiple Series. If tag id is 0, this will create a new tag.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update-for-series")]
    public async Task<ActionResult> AddToMultipleSeries(CollectionTagBulkAddDto dto)
    {
        // Create a new tag and save
        var tag = await _collectionService.GetTagOrCreate(dto.CollectionTagId, dto.CollectionTagTitle);

        if (await _collectionService.AddTagToSeries(tag, dto.SeriesIds)) return Ok();

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
            var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(updateSeriesForTagDto.Tag.Id, CollectionTagIncludes.SeriesMetadata);
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
    /// Removes the collection tag from all Series it was attached to
    /// </summary>
    /// <param name="tagId"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete]
    public async Task<ActionResult> DeleteTag(int tagId)
    {
        try
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(tagId, CollectionTagIncludes.SeriesMetadata);
            if (tag == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "collection-doesnt-exist"));

            if (await _collectionService.DeleteTag(tag))
                return Ok(await _localizationService.Translate(User.GetUserId(), "collection-deleted"));
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
    }
}
