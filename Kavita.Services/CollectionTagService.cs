using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using Kavita.Models.Extensions;

namespace Kavita.Services;

public class CollectionTagService(IUnitOfWork unitOfWork, IEventHub eventHub, IDirectoryService directoryService) : ICollectionTagService
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
        var existingTag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(dto.Id, ct: ct);
        if (existingTag == null) throw new KavitaException("collection-doesnt-exist");
        if (existingTag.AppUserId != userId) throw new KavitaException("access-denied");

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
        existingTag.CoverImageLocked = dto.CoverImageLocked;
        unitOfWork.CollectionTagRepository.Update(existingTag);

        // Check if Tag has updated (Summary)
        var summary = (dto.Summary ?? string.Empty).Trim();
        if (existingTag.Summary == null || !existingTag.Summary.Equals(summary))
        {
            existingTag.Summary = summary;
            unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        // If we unlock the cover image, it means reset
        if (!dto.CoverImageLocked)
        {
            existingTag.CoverImageLocked = false;
            existingTag.CoverImage = string.Empty;
            await eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(existingTag.Id, MessageFactoryEntityTypes.Collection), false, ct);
            unitOfWork.CollectionTagRepository.Update(existingTag);
        }

        if (!unitOfWork.HasChanges()) return true;
        return await unitOfWork.CommitAsync(ct);
    }

    public async Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds, CancellationToken ct = default)
    {
        if (tag == null) return false;

        tag.Items ??= [];
        tag.Items = tag.Items.Where(s => !seriesIds.Contains(s.Id)).ToList();

        if (tag.Items.Count == 0)
        {
            unitOfWork.CollectionTagRepository.Remove(tag);
        }

        if (!unitOfWork.HasChanges()) return true;

        var result  = await unitOfWork.CommitAsync(ct);
        if (tag.Items.Count > 0)
        {
            await unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(tag, ct);
        }

        return result;
    }

    public async Task<string> GenerateCollectionCoverImage(int collectionId)
    {
        var covers = await unitOfWork.CollectionTagRepository.GetRandomCoverImagesAsync(collectionId);
        var destFile = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, ImageService.GetCollectionTagFormat(collectionId));

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        destFile += settings.EncodeMediaAs.GetExtension();

        if (directoryService.FileSystem.File.Exists(destFile)) return destFile;

        ImageService.CreateMergedImage(
            covers.Select(c => directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory, c)).ToList(),
            settings.CoverImageSize,
            destFile);

        // TODO: Refactor this so that collections have a dedicated cover image so we can calculate primary/secondary colors
        return !directoryService.FileSystem.File.Exists(destFile) ? string.Empty : destFile;
    }
}
