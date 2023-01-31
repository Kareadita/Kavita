using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Entities.Metadata;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;


public interface ICollectionTagService
{
    Task<bool> TagExistsByName(string name);
    Task<bool> UpdateTag(CollectionTagDto dto);
    Task<bool> AddTagToSeries(CollectionTag tag, IEnumerable<int> seriesIds);
    Task<bool> RemoveTagFromSeries(CollectionTag tag, IEnumerable<int> seriesIds);
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
    public async Task<bool> TagExistsByName(string name)
    {
        if (string.IsNullOrEmpty(name.Trim())) return true;
        return await _unitOfWork.CollectionTagRepository.TagExists(name);
    }

    public async Task<bool> UpdateTag(CollectionTagDto dto)
    {
        var existingTag = await _unitOfWork.CollectionTagRepository.GetTagAsync(dto.Id);
        if (existingTag == null) throw new KavitaException("This tag does not exist");

        var title = dto.Title.Trim();
        if (string.IsNullOrEmpty(title)) throw new KavitaException("Title cannot be empty");
        if (!title.Equals(existingTag.Title) && await TagExistsByName(dto.Title))
            throw new KavitaException("A tag with this name already exists");

        existingTag.SeriesMetadatas ??= new List<SeriesMetadata>();
        existingTag.Title = title;
        existingTag.NormalizedTitle = Tasks.Scanner.Parser.Parser.Normalize(dto.Title);
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
    public async Task<bool> AddTagToSeries(CollectionTag tag, IEnumerable<int> seriesIds)
    {
        var metadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(seriesIds);
        foreach (var metadata in metadatas)
        {
            if (!metadata.CollectionTags.Any(t => t.Title.Equals(tag.Title, StringComparison.InvariantCulture)))
            {
                metadata.CollectionTags.Add(tag);
                _unitOfWork.SeriesMetadataRepository.Update(metadata);
            }
        }

        if (!_unitOfWork.HasChanges()) return true;
        return await _unitOfWork.CommitAsync();
    }

    public async Task<bool> RemoveTagFromSeries(CollectionTag tag, IEnumerable<int> seriesIds)
    {
        foreach (var seriesIdToRemove in seriesIds)
        {
            tag.SeriesMetadatas.Remove(tag.SeriesMetadatas.Single(sm => sm.SeriesId == seriesIdToRemove));
        }


        if (tag.SeriesMetadatas.Count == 0)
        {
            _unitOfWork.CollectionTagRepository.Remove(tag);
        }

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }
}
