using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.Services;

public class CollectionTagService(IUnitOfWork unitOfWork, IEventHub eventHub) : ICollectionTagService
{
    public async Task<bool> DeleteTag(int tagId, AppUser user)
    {
        var collectionTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(tagId);
        if (collectionTag == null) return true;

        user.Collections.Remove(collectionTag);

        if (!unitOfWork.HasChanges()) return true;

        return await unitOfWork.CommitAsync();
    }


    public async Task<bool> UpdateTag(AppUserCollectionDto dto, int userId)
    {
        var existingTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(dto.Id);
        if (existingTag == null) throw new KavitaException("collection-doesnt-exist");
        if (existingTag.AppUserId != userId) throw new KavitaException("access-denied");

        var title = dto.Title.Trim();
        if (string.IsNullOrEmpty(title)) throw new KavitaException("collection-tag-title-required");

        // Ensure the title doesn't exist on the user's account already
        if (!title.Equals(existingTag.Title) && await unitOfWork.CollectionTagRepository.CollectionExists(dto.Title, userId))
            throw new KavitaException("collection-tag-duplicate");

        existingTag.Items ??= [];
        if (existingTag.Source == ScrobbleProvider.Kavita)
        {
            existingTag.Title = title;
            existingTag.NormalizedTitle = dto.Title.ToNormalized();
        }

        var roles = await unitOfWork.UserRepository.GetRoles(userId);
        if (roles.Contains(PolicyConstants.AdminRole) || roles.Contains(PolicyConstants.PromoteRole))
        {
            existingTag.Promoted = dto.Promoted;
        }
        existingTag.CoverImageLocked = dto.CoverImageLocked;
        unitOfWork.CollectionTagRepository.Update(existingTag);

        // Check if Tag has updated (Summary)
        var summary = (dto.Summary ?? string.Empty).Trim();
        if (existingTag.Summary == null || !existingTag.Summary.Equals(summary))
        {
            existingTag.Summary = summary;
            unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        // If we unlock the cover image it means reset
        if (!dto.CoverImageLocked)
        {
            existingTag.CoverImageLocked = false;
            existingTag.CoverImage = string.Empty;
            await eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(existingTag.Id, MessageFactoryEntityTypes.Collection), false);
            unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        if (!unitOfWork.HasChanges()) return true;
        return await unitOfWork.CommitAsync();
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

        tag.Items ??= [];
        tag.Items = tag.Items.Where(s => !seriesIds.Contains(s.Id)).ToList();

        if (tag.Items.Count == 0)
        {
            unitOfWork.CollectionTagRepository.Remove(tag);
        }

        if (!unitOfWork.HasChanges()) return true;

        var result  =  await unitOfWork.CommitAsync();
        if (tag.Items.Count > 0)
        {
            await unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(tag);
        }

        return result;
    }
}
