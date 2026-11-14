using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Images;

public class ImageFileService(
    IUnitOfWork unitOfWork,
    IImagePipeline imagePipeline,
    IDirectoryService directoryService,
    ILogger<ImageFileService> logger) : IImageFileService
{
    private const float PlaceholderSimilarity = 0.9f;
    private static readonly ImageKind[] SharedKinds = [ImageKind.ChapterCover, ImageKind.VolumeCover, ImageKind.SeriesCover];

    public async Task SetCoverAsync(IHasCoverImageFile entity, ImageSource source, CoverWrite write, bool commit = true,
        CancellationToken ct = default)
    {
        EnsureCoverLoaded(entity);

        if ((write.ChooseBetter || write.RejectPlaceholder) && source is not ImageSource.FromFile)
        {
            throw new ArgumentException("ChooseBetter and RejectPlaceholder compare files, so they need a file source", nameof(write));
        }

        var target = Describe(entity);
        var current = entity.CoverImageFile;

        if (write.RejectPlaceholder && IsPlaceholder(((ImageSource.FromFile) source).Path, entity)) return;
        if (write.ChooseBetter && current != null && ExistingIsBetter(current, ((ImageSource.FromFile) source).Path, entity)) return;

        var lockable = entity as IHasLockableCoverImageFile;
        if (lockable != null && !write.Lock)
        {
            // Unlock before the write: a locked volume/series sharing this cover must copy the current art, not the new one
            await ApplyLockAsync(lockable, false, ct);
        }

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new ImagePolicy(settings.EncodeMediaAs, settings.CoverImageSize);
        var format = policy.Format.ForKind(target.Kind);
        var fileName = target.Stem + format.GetExtension();

        var row = current != null && current.Kind == target.Kind
            ? current
            : await unitOfWork.ImageFileRepository.GetByNameAsync(target.Kind, fileName, ct);
        var oldFileName = row?.FileName;

        var written = imagePipeline.Write(source, target.Kind, policy, directoryService.CoverImageDirectory, fileName);
        var colors = ImageService.CalculateColorScape(Path.Join(directoryService.CoverImageDirectory, written.FileName));

        if (row == null)
        {
            row = new ImageFile { Kind = target.Kind, FileName = written.FileName };
            unitOfWork.ImageFileRepository.Add(row);
        }

        row.FileName = written.FileName;
        row.Origin = write.Origin;
        row.Format = format;
        row.Width = written.Width;
        row.Height = written.Height;
        row.SourceWidth = written.SourceWidth;
        row.SourceHeight = written.SourceHeight;
        row.PrimaryColor = colors.Primary;
        row.SecondaryColor = colors.Secondary;

        Link(entity, row);
        if (lockable != null && write.Lock)
        {
            await ApplyLockAsync(lockable, true, ct);
        }

        // Until I update read code, keep the entity CoverImage inline for testing
        foreach (var holder in await GetHoldersAsync(row, [entity], ct))
        {
            SyncLegacyCover(holder, row);
        }

        var staleFiles = oldFileName != null && oldFileName != row.FileName ? new List<string> { oldFileName } : [];
        await FinishAsync(commit, staleFiles, ct);
    }

    public async Task ShareCoverAsync(IHasCoverImageFile target, IHasCoverImageFile source, bool lockBoth, bool commit = true,
        CancellationToken ct = default)
    {
        EnsureCoverLoaded(target);
        EnsureCoverLoaded(source);

        var row = source.CoverImageFile;
        if (row == null) return;

        var previous = target.CoverImageFile;
        Link(target, row);
        SyncLegacyCover(target, row);

        if (lockBoth)
        {
            if (target is IHasLockableCoverImageFile lockableTarget)
            {
                await ApplyLockAsync(lockableTarget, true, ct);
            }

            if (source is IHasLockableCoverImageFile lockableSource)
            {
                await ApplyLockAsync(lockableSource, true, ct);
            }
        }

        var staleFiles = new List<string>();
        if (!ReferenceEquals(previous, row))
        {
            await ReleaseIfUnheldAsync(target, previous, staleFiles, ct);
        }

        await FinishAsync(commit, staleFiles, ct);
    }

    public async Task ClearCoverAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default)
    {
        EnsureCoverLoaded(entity);

        if (entity is IHasLockableCoverImageFile lockable)
        {
            await ApplyLockAsync(lockable, false, ct);
        }

        var previous = entity.CoverImageFile;
        entity.CoverImageFile = null;
        entity.CoverImageFileId = null;
        SyncLegacyCover(entity, null);

        var staleFiles = new List<string>();
        await ReleaseIfUnheldAsync(entity, previous, staleFiles, ct);
        await FinishAsync(commit, staleFiles, ct);
    }

    public async Task UpdateLockAsync(IHasLockableCoverImageFile entity, bool locked, bool commit = true,
        CancellationToken ct = default)
    {
        EnsureCoverLoaded(entity);
        await ApplyLockAsync(entity, locked, ct);
        await FinishAsync(commit, [], ct);
    }

    private async Task ApplyLockAsync(IHasLockableCoverImageFile entity, bool locked, CancellationToken ct)
    {
        entity.CoverImageLocked = locked;

        var row = entity.CoverImageFile;
        if (row == null) return;

        var ownsRow = row.Kind == Describe(entity).Kind;
        var holders = await GetHoldersAsync(row, [entity], ct);

        if (locked)
        {
            if (ownsRow) return;

            foreach (var owner in holders.OfType<IHasLockableCoverImageFile>().Where(h => Describe(h).Kind == row.Kind))
            {
                owner.CoverImageLocked = true;
            }

            return;
        }

        if (!ownsRow) return;

        // Sharers are volumes/series that share a cover from a chapter/volume; a locked one is unshared so it keeps its art
        var lockedSharers = holders
            .OfType<IHasLockableCoverImageFile>()
            .Where(h => !ReferenceEquals(h, entity) && h.CoverImageLocked && Describe(h).Kind != row.Kind)
            .ToList();

        foreach (var sharer in lockedSharers)
        {
            await UnshareCoverAsync(sharer, row, ct);
        }
    }

    private async Task UnshareCoverAsync(IHasCoverImageFile sharer, ImageFile source, CancellationToken ct)
    {
        var sourcePath = Path.Join(directoryService.CoverImageDirectory, source.FileName);
        if (!File.Exists(sourcePath))
        {
            logger.LogWarning("Could not give {Type} {Id} its own cover: {File} is missing, it stays on the shared row",
                sharer.GetType().Name, sharer.Id, source.FileName);
            return;
        }

        var target = Describe(sharer);
        var fileName = target.Stem + Path.GetExtension(source.FileName);
        var existing = await unitOfWork.ImageFileRepository.GetByNameAsync(target.Kind, fileName, ct);

        var written = imagePipeline.Copy(sourcePath, target.Kind, directoryService.CoverImageDirectory, target.Stem);

        var copy = existing ?? new ImageFile { Kind = target.Kind, FileName = written.FileName };
        if (existing == null)
        {
            unitOfWork.ImageFileRepository.Add(copy);
        }

        copy.FileName = written.FileName;
        copy.Origin = source.Origin;
        copy.Format = source.Format;
        copy.Width = source.Width;
        copy.Height = source.Height;
        copy.SourceWidth = source.SourceWidth;
        copy.SourceHeight = source.SourceHeight;
        copy.PrimaryColor = source.PrimaryColor;
        copy.SecondaryColor = source.SecondaryColor;

        Link(sharer, copy);
        SyncLegacyCover(sharer, copy);
    }

    private async Task ReleaseIfUnheldAsync(IHasCoverImageFile entity, ImageFile? previous, List<string> staleFiles,
        CancellationToken ct)
    {
        if (previous == null || previous.Kind != Describe(entity).Kind) return;
        if ((await GetHoldersAsync(previous, [], ct)).Count > 0) return;

        unitOfWork.ImageFileRepository.Remove(previous);
        staleFiles.Add(previous.FileName);
    }

    // Filtered on the in-memory navigation: until the commit the query still lists holders this call moved off the row
    private async Task<List<IHasCoverImageFile>> GetHoldersAsync(ImageFile row, IEnumerable<IHasCoverImageFile> linkedNow,
        CancellationToken ct)
    {
        var candidates = new List<IHasCoverImageFile>(linkedNow);
        if (row.Id != 0 && SharedKinds.Contains(row.Kind))
        {
            var stored = await unitOfWork.ImageFileRepository.GetHoldersAsync(row.Id, ct);
            candidates.AddRange(stored.Chapters);
            candidates.AddRange(stored.Volumes);
            candidates.AddRange(stored.Series);
        }

        return candidates
            .Where(h => ReferenceEquals(h.CoverImageFile, row))
            .Distinct()
            .ToList();
    }

    private async Task FinishAsync(bool commit, List<string> staleFiles, CancellationToken ct)
    {
        if (!commit) return;

        await unitOfWork.CommitAsync(ct);

        foreach (var fileName in staleFiles)
        {
            DeleteQuietly(Path.Join(directoryService.CoverImageDirectory, fileName));
        }
    }

    private bool IsPlaceholder(string sourcePath, IHasCoverImageFile entity)
    {
        var placeholderPath = Path.Join(directoryService.AssetsDirectory, "anilist-no-image-placeholder.jpg");
        var similarity = placeholderPath.CalculateSimilarity(sourcePath);
        if (similarity < PlaceholderSimilarity) return false;

        logger.LogInformation("Skipped a placeholder cover for {Type} {Id} (similarity {Similarity})",
            entity.GetType().Name, entity.Id, similarity);
        return true;
    }

    private bool ExistingIsBetter(ImageFile current, string sourcePath, IHasCoverImageFile entity)
    {
        var existingPath = Path.Join(directoryService.CoverImageDirectory, current.FileName);
        if (!File.Exists(existingPath))
        {
            logger.LogWarning("Existing cover for {Type} {Id} is missing on disk ({File}); accepting the new cover",
                entity.GetType().Name, entity.Id, current.FileName);
            return false;
        }

        try
        {
            var better = existingPath.GetBetterImage(sourcePath);
            return !string.Equals(better, sourcePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not compare covers for {Type} {Id}; accepting the new cover",
                entity.GetType().Name, entity.Id);
            return false;
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete replaced cover {File}", path);
        }
    }

    private static void EnsureCoverLoaded(IHasCoverImageFile entity)
    {
        if (entity is {CoverImageFileId: not null, CoverImageFile: null})
        {
            throw new InvalidOperationException(
                $"{entity.GetType().Name} {entity.Id} was loaded without its CoverImageFile include");
        }
    }

    private static void Link(IHasCoverImageFile entity, ImageFile row)
    {
        entity.CoverImageFile = row;

        if (row.Id != 0)
        {
            entity.CoverImageFileId = row.Id;
        }
    }


    private static void SyncLegacyCover(IHasCoverImageFile holder, ImageFile? row)
    {
        if (holder is not IHasCoverImage legacy) return;

        legacy.CoverImage = row?.FileName;
        legacy.PrimaryColor = row?.PrimaryColor;
        legacy.SecondaryColor = row?.SecondaryColor;
    }


    private static CoverTarget Describe(IHasCoverImageFile entity) => entity switch
    {
        Chapter chapter => new CoverTarget(ImageKind.ChapterCover, ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId)),
        Volume volume => new CoverTarget(ImageKind.VolumeCover, ImageService.GetVolumeFormat(volume.Id)),
        Series series => new CoverTarget(ImageKind.SeriesCover, ImageService.GetSeriesFormat(series.Id)),
        Library library => new CoverTarget(ImageKind.LibraryIcon, ImageService.GetLibraryFormat(library.Id)),
        Person person => new CoverTarget(ImageKind.PersonCover, ImageService.GetPersonFormat(person.Id)),
        ReadingList readingList => new CoverTarget(ImageKind.ReadingListCover, ImageService.GetReadingListFormat(readingList.Id)),
        AppUser user => new CoverTarget(ImageKind.UserCover, ImageService.GetUserFormat(user.Id)),
        AppUserCollection collection => new CoverTarget(ImageKind.CollectionCover, ImageService.GetCollectionTagFormat(collection.Id)),
        _ => throw new ArgumentOutOfRangeException(nameof(entity), entity.GetType().Name, null),
    };

    private readonly record struct CoverTarget(ImageKind Kind, string Stem);
}
