using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Collection;
using API.Entities;
using API.Extensions;
using API.Services.Plus;
using API.SignalR;
using Kavita.Common;

namespace API.Services;
#nullable enable

public interface ICollectionTagService
{
    Task<bool> DeleteTag(int tagId, AppUser user);
    Task<bool> UpdateTag(AppUserCollectionDto dto, int userId);
    Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds);
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
        if (existingTag.AppUserId != userId) throw new KavitaException("access-denied");

        var title = dto.Title.Trim();
        if (string.IsNullOrEmpty(title)) throw new KavitaException("collection-tag-title-required");

        // Ensure the title doesn't exist on the user's account already
        if (!title.Equals(existingTag.Title) && await _unitOfWork.CollectionTagRepository.CollectionExists(dto.Title, userId))
            throw new KavitaException("collection-tag-duplicate");

        existingTag.Items ??= new List<Series>();
        if (existingTag.Source == ScrobbleProvider.Kavita)
        {
            existingTag.Title = title;
            existingTag.NormalizedTitle = dto.Title.ToNormalized();
        }

        var roles = await _unitOfWork.UserRepository.GetRoles(userId);
        if (roles.Contains(PolicyConstants.AdminRole) || roles.Contains(PolicyConstants.PromoteRole))
        {
            existingTag.Promoted = dto.Promoted;
        }
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
    /// Removes series from Collection tag. Will recalculate max age rating.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public async Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds)
    {
        if (tag == null) return false;

        tag.Items ??= new List<Series>();
        tag.Items = tag.Items.Where(s => !seriesIds.Contains(s.Id)).ToList();

        if (tag.Items.Count == 0)
        {
            _unitOfWork.CollectionTagRepository.Remove(tag);
        }

        if (!_unitOfWork.HasChanges()) return true;

        var result  =  await _unitOfWork.CommitAsync();
        if (tag.Items.Count > 0)
        {
            await _unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(tag);
        }

        return result;
    }
}
