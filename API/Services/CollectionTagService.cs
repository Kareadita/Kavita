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
using API.SignalR;
using Kavita.Common;

namespace API.Services;
#nullable enable

public interface ICollectionTagService
{
    //Task<bool> TagExistsByName(string name);
    //Task<bool> DeleteTag(CollectionTag tag);
    Task<bool> DeleteTag(int tagId, AppUser user);
    Task<bool> UpdateTag(AppUserCollectionDto dto, int userId);
    // Task<bool> AddTagToSeries(CollectionTag? tag, IEnumerable<int> seriesIds);
    Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds);
    //Task<CollectionTag> GetTagOrCreate(int tagId, string title);
    Task AddTagToSeriesMetadata(AppUserCollection? tag, Series series);
    //CollectionTag CreateTag(string title);
    Task<bool> RemoveTagsWithoutSeries();
}


public class CollectionTagService : ICollectionTagService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;

    public CollectionTagService(IUnitOfWork unitOfWork, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Checks if a collection exists with the name
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    // public async Task<bool> TagExistsByName(string name)
    // {
    //     if (string.IsNullOrEmpty(name.Trim())) return true;
    //     return await _unitOfWork.CollectionTagRepository.TagExists(name);
    // }

    public async Task<bool> DeleteTag(CollectionTag tag)
    {
        _unitOfWork.CollectionTagRepository.Remove(tag);
        return await _unitOfWork.CommitAsync();
    }

    public async Task<bool> DeleteTag(int tagId, AppUser user)
    {
        var collectionTag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(tagId);
        if (collectionTag == null) return true;
        user.Collections.Remove(collectionTag);

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }


    public async Task<bool> UpdateTag(AppUserCollectionDto dto, int userId)
    {
        var existingTag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(dto.Id);
        if (existingTag == null) throw new KavitaException("collection-doesnt-exist");

        var title = dto.Title.Trim();
        if (string.IsNullOrEmpty(title)) throw new KavitaException("collection-tag-title-required");

        // Ensure the title doesn't exist on the user's account already
        if (!title.Equals(existingTag.Title) && await _unitOfWork.CollectionTagRepository.TagExists(dto.Title, userId))
            throw new KavitaException("collection-tag-duplicate");

        existingTag.Items ??= new List<Series>();
        existingTag.Title = title;
        existingTag.NormalizedTitle = dto.Title.ToNormalized();
        existingTag.Promoted = dto.Promoted;
        existingTag.CoverImageLocked = dto.CoverImageLocked;
        _unitOfWork.CollectionTagRepository.Update(existingTag);

        // Check if Tag has updated (Summary)
        var summary = dto.Summary.Trim();
        if (existingTag.Summary == null || !existingTag.Summary.Equals(summary))
        {
            existingTag.Summary = summary;
            _unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        // If we unlock the cover image it means reset
        if (!dto.CoverImageLocked)
        {
            existingTag.CoverImageLocked = false;
            existingTag.CoverImage = string.Empty;
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(existingTag.Id, MessageFactoryEntityTypes.CollectionTag), false);
            _unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        if (!_unitOfWork.HasChanges()) return true;
        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Adds a set of Series to a Collection
    /// </summary>
    /// <param name="tag">A full Tag</param>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public Task<bool> AddTagToSeries(CollectionTag? tag, IEnumerable<int> seriesIds)
    {
        return Task.FromResult(false);
        //if (tag == null) return false;
        // var metadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(seriesIds);
        // foreach (var metadata in metadatas)
        // {
        //     await AddTagToSeriesMetadata(tag, metadata);
        // }
        //
        // if (!_unitOfWork.HasChanges()) return true;
        // return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Adds a collection tag to a SeriesMetadata
    /// </summary>
    /// <remarks>Does not commit</remarks>
    /// <param name="tag"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public async Task AddTagToSeriesMetadata(AppUserCollection? tag, Series series)
    {
        if (tag == null) return;

        var user = await _unitOfWork.UserRepository.GetDefaultAdminUser(AppUserIncludes.Collections);
        if (user.Collections.Any(t =>
                t.NormalizedTitle.Equals(tag.NormalizedTitle, StringComparison.InvariantCulture))) return;

        user.Collections.Add(tag);
        tag.Items.Add(series);


        // metadata.CollectionTags ??= new List<CollectionTag>();
        // if (metadata.CollectionTags.Any(t => t.NormalizedTitle.Equals(tag.NormalizedTitle, StringComparison.InvariantCulture))) return;
        //
        // metadata.CollectionTags.Add(tag);
        // if (metadata.Id != 0)
        // {
        //     _unitOfWork.SeriesMetadataRepository.Update(metadata);
        // }
    }

    public async Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds)
    {
        if (tag == null) return false;


        tag.Items ??= new List<Series>();
        foreach (var seriesIdToRemove in seriesIds)
        {
            tag.Items.Remove(tag.Items.Single(sm => sm.Id == seriesIdToRemove));
        }


        if (tag.Items.Count == 0)
        {
            _unitOfWork.CollectionTagRepository.Remove(tag);
        }

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Tries to fetch the full tag, else returns a new tag. Adds to tracking but does not commit
    /// </summary>
    /// <param name="tagId"></param>
    /// <param name="title"></param>
    /// <returns></returns>
    // public async Task<CollectionTag> GetTagOrCreate(int tagId, string title)
    // {
    //     return await _unitOfWork.CollectionTagRepository.GetTagAsync(tagId, CollectionTagIncludes.SeriesMetadata) ?? CreateTag(title);
    // }

    /// <summary>
    /// This just creates the entity and adds to tracking. Use <see cref="GetTagOrCreate"/> for checks of duplication.
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    // public AppUserCollection CreateTag(string title)
    // {
    //     var tag = new AppUserCollectionBuilder(title).Build();
    //     _unitOfWork.CollectionTagRepository.Add(tag);
    //     return tag;
    // }

    public async Task<bool> RemoveTagsWithoutSeries()
    {
        return await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries() > 0;
    }
}
