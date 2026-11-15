using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Images;

public class CompositeCoverService(
    IUnitOfWork unitOfWork,
    IImageFileService imageFileService,
    IDirectoryService directoryService,
    ILogger<CompositeCoverService> logger) : ICompositeCoverService
{
    private const int MaxTiles = 4;

    public async Task RefreshAsync(ImageKind kind, int id, bool force, CancellationToken ct = default)
    {
        var target = await LoadAsync(kind, id, ct);
        if (target == null) return;

        var (entity, itemCovers) = target.Value;
        if (entity.CoverImageLocked) return;

        var row = entity.CoverImageFile;
        if (row != null && row.Origin != ImageOrigin.Composite) return;

        var tiles = itemCovers
            .Select(f => Path.Join(directoryService.CoverImageDirectory, f.FileName))
            .Where(File.Exists)
            .ToList();

        if (tiles.Count == 0)
        {
            if (row != null)
            {
                await imageFileService.ClearCoverAsync(entity, ct: ct);
            }

            return;
        }

        var hash = ComputeInputHash(itemCovers);
        if (!force && row?.InputHash == hash) return;

        var picked = tiles.OrderBy(_ => Random.Shared.Next()).Take(MaxTiles).ToList();
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var buffer = ImageService.CreateMergedImageBuffer(picked, settings.CoverImageSize);

        await imageFileService.SetCoverAsync(entity, new ImageSource.FromBytes(buffer),
            new CoverWrite(ImageOrigin.Composite, Lock: false, InputHash: hash), ct: ct);

        logger.LogDebug("Built a {Kind} for {Id} from {Count} item covers", kind, id, picked.Count);
    }

    private async Task<(IHasLockableCoverImageFile Entity, IList<ImageFile> ItemCovers)?> LoadAsync(ImageKind kind, int id,
        CancellationToken ct)
    {
        switch (kind)
        {
            case ImageKind.ReadingListCover:
            {
                var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(id,
                    ReadingListIncludes.CoverImageFile, ct);
                if (readingList == null) return null;
                return (readingList, await unitOfWork.ReadingListRepository.GetItemCoverFilesAsync(id, ct));
            }
            case ImageKind.CollectionCover:
            {
                var collection = await unitOfWork.CollectionTagRepository.GetCollectionAsync(id,
                    CollectionIncludes.CoverImageFile, ct);
                if (collection == null) return null;
                return (collection, await unitOfWork.CollectionTagRepository.GetItemCoverFilesAsync(id, ct));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only reading list and collection covers are composite");
        }
    }

    private static string ComputeInputHash(IList<ImageFile> itemCovers)
    {
        var parts = itemCovers
            .OrderBy(f => f.Id)
            .Select(f => f.Id.ToString(CultureInfo.InvariantCulture) + ":" + f.LastModifiedUtc.ToString("O", CultureInfo.InvariantCulture));

        return FileService.ComputeSha256(itemCovers.Count.ToString(CultureInfo.InvariantCulture) + "|" + string.Join("|", parts));
    }
}
