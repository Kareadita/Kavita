using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;

namespace Kavita.Services;

public class CollectionTagService(IUnitOfWork unitOfWork, IImageFileService imageFileService,
    ICompositeCoverService compositeCoverService) : ICollectionTagService
{
    public async Task<bool> DeleteTag(int tagId, AppUser user, CancellationToken ct = default)
    {
        var collectionTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(tagId, ct: ct);
        if (collectionTag == null) return true;

        user.Collections.Remove(collectionTag);

        if (!unitOfWork.HasChanges()) return true;

        return await unitOfWork.CommitAsync(ct);
    }


    public async Task<bool> UpdateTag(AppUserCollectionDto dto, int userId, CancellationToken ct = default)
    {
        var existingTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(dto.Id, CollectionIncludes.CoverImageFile, ct);
        if (existingTag == null) throw new KavitaException("collection-doesnt-exist");

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) throw new KavitaException("user-doesnt-exist");

        var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user, ct);
        if (existingTag.AppUserId != userId && !isAdmin) throw new KavitaException("access-denied");

        var title = dto.Title.Trim();
        if (string.IsNullOrEmpty(title)) throw new KavitaException("collection-tag-title-required");

        // Ensure the title doesn't exist on the user's account already
        if (!title.Equals(existingTag.Title) && await unitOfWork.CollectionTagRepository.CollectionExists(dto.Title, userId, ct))
            throw new KavitaException("collection-tag-duplicate");

        existingTag.Items ??= [];
        if (existingTag.Source == ScrobbleProvider.Kavita)
        {
            existingTag.Title = title;
            existingTag.NormalizedTitle = dto.Title.ToNormalized();
        }

        var roles = await unitOfWork.UserRepository.GetRoles(userId, ct);
        if (roles.Contains(PolicyConstants.AdminRole) || roles.Contains(PolicyConstants.PromoteRole))
        {
            existingTag.Promoted = dto.Promoted;
        }
        var resetCover = existingTag.CoverImageLocked && !dto.CoverImageLocked;
        if (!resetCover)
        {
            await imageFileService.UpdateLockAsync(existingTag, dto.CoverImageLocked, commit: false, ct: ct);
        }
        unitOfWork.CollectionTagRepository.Update(existingTag);

        // Check if Tag has updated (Summary)
        var summary = (dto.Summary ?? string.Empty).Trim();
        if (existingTag.Summary == null || !existingTag.Summary.Equals(summary))
        {
            existingTag.Summary = summary;
            unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        var saved = !unitOfWork.HasChanges() || await unitOfWork.CommitAsync(ct);

        if (resetCover)
        {
            await imageFileService.ClearCoverAsync(existingTag, ct: ct);
            await compositeCoverService.CreateIfMissingAsync(ImageKind.CollectionCover, existingTag.Id, ct);
        }

        return saved;
    }

    public async Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds, CancellationToken ct = default)
    {
        if (tag == null) return false;

        tag.Items ??= [];
        var previousItemCount = tag.Items.Count;
        tag.Items = tag.Items.Where(s => !seriesIds.Contains(s.Id)).ToList();

        if (tag.Items.Count == 0)
        {
            unitOfWork.CollectionTagRepository.Remove(tag);
        }
        else if (tag.Items.Count < previousItemCount)
        {
            unitOfWork.CollectionTagRepository.Update(tag);
        }

        if (!unitOfWork.HasChanges()) return true;

        var result  = await unitOfWork.CommitAsync(ct);
        if (tag.Items.Count > 0)
        {
            await unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(tag, ct);
        }

        return result;
    }
}
