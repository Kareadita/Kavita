using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Microsoft.Extensions.Logging;
using Kavita.Services.Helpers;

namespace Kavita.Services.Images;

public class ImageFileService(
    IUnitOfWork unitOfWork,
    IImagePipeline imagePipeline,
    IDirectoryService directoryService,
    IEventHub eventHub,
    ILogger<ImageFileService> logger) : IImageFileService
{
    private const float PlaceholderSimilarity = 0.9f;
    private static readonly ImageKind[] SharedKinds = [ImageKind.ChapterCover, ImageKind.VolumeCover, ImageKind.SeriesCover];

    private readonly List<string> _replacedFiles = [];

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
            // Unlock before writing as a locked volume/series sharing this cover must use the current cover
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
        var colors = ImageHelper.CalculateColorScape(Path.Join(directoryService.CoverImageDirectory, written.FileName));

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
        row.InputHash = write.InputHash;
        // A rewrite with the same size and colors changes no column, and composite covers read this to see a new cover
        row.LastModified = DateTime.Now;
        row.LastModifiedUtc = DateTime.UtcNow;

        Link(entity, row);
        if (lockable != null && write.Lock)
        {
            await ApplyLockAsync(lockable, true, ct);
        }

        var holders = await GetHoldersAsync(row, [entity], ct);
        var staleFiles = oldFileName != null && oldFileName != row.FileName ? new List<string> { oldFileName } : [];

        await FinishAsync(commit, staleFiles, holders, ct);
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
            await ReleaseIfUnheldAsync(previous, staleFiles, ct);
        }

        await FinishAsync(commit, staleFiles, [target], ct);
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

        var staleFiles = new List<string>();
        await ReleaseIfUnheldAsync(previous, staleFiles, ct);
        await FinishAsync(commit, staleFiles, [entity], ct);
    }

    public async Task RefreshColorsAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default)
    {
        EnsureCoverLoaded(entity);

        var row = entity.CoverImageFile;
        if (row == null) return;

        var path = Path.Join(directoryService.CoverImageDirectory, row.FileName);
        if (!File.Exists(path)) return;

        var colors = ImageHelper.CalculateColorScape(path);
        row.PrimaryColor = colors.Primary;
        row.SecondaryColor = colors.Secondary;

        var holders = await GetHoldersAsync(row, [entity], ct);

        await FinishAsync(commit, [], holders, ct);
    }

    public void DeleteReplacedFiles()
    {
        foreach (var fileName in _replacedFiles)
        {
            directoryService.DeleteFileQuietly(Path.Join(directoryService.CoverImageDirectory, fileName));
        }

        _replacedFiles.Clear();
    }

    public async Task UpdateLockAsync(IHasLockableCoverImageFile entity, bool locked, bool commit = true,
        CancellationToken ct = default)
    {
        EnsureCoverLoaded(entity);
        await ApplyLockAsync(entity, locked, ct);
        await FinishAsync(commit, [], [], ct);
    }

    public async Task<string> WriteKeyedAsync(ImageSource source, ImageKind kind, string key, string fileName,
        ImageOrigin origin, CancellationToken ct = default)
    {
        if (kind is not (ImageKind.Favicon or ImageKind.Publisher))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only favicons and publishers are keyed");
        }

        // The name is built from a request value (a host or a publisher name), so it must not reach outside the folder
        if (Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException($"A keyed image name cannot contain a folder: {fileName}", nameof(fileName));
        }

        var directory = await GetImageDirectoryAsync(kind, ct);

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new ImagePolicy(settings.EncodeMediaAs, CoverImageSize.Default);
        var written = imagePipeline.Write(source, kind, policy, directory, fileName);

        // We need to handle a case where a user changes the EncodeFormat, then visits a page which downloads the new domain.
        // The domain now is going to be anilist.co.webp (switched from png -> webp) and thus the row wont find, because the old row is
        // still anilist.co.png and hasn't been converted yet
        var row = await unitOfWork.ImageFileRepository.GetByNameAsync(kind, written.FileName, ct)
                  ?? await unitOfWork.ImageFileRepository.GetByKeyAsync(kind, key, ct);
        if (row == null)
        {
            row = new ImageFile { Kind = kind, FileName = written.FileName };
            unitOfWork.ImageFileRepository.Add(row);
        }

        var replacedFileName = row.FileName != written.FileName ? row.FileName : null;
        row.FileName = written.FileName;
        row.Key = key;
        row.Origin = origin;
        row.Format = policy.Format.ForKind(kind);
        row.Width = written.Width;
        row.Height = written.Height;
        row.SourceWidth = written.SourceWidth;
        row.SourceHeight = written.SourceHeight;

        await unitOfWork.CommitAsync(ct);

        if (replacedFileName != null)
        {
            directoryService.DeleteFileQuietly(Path.Join(directory, replacedFileName));
        }

        return written.FileName;
    }

    public async Task<string> GetImageDirectoryAsync(ImageKind kind, CancellationToken ct = default)
    {
        return kind switch
        {
            ImageKind.Favicon => directoryService.FaviconDirectory,
            ImageKind.Publisher => directoryService.PublisherDirectory,
            ImageKind.Bookmark => (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory, ct)).Value,
            _ => directoryService.CoverImageDirectory,
        };
    }

    public bool HasCoverFile(IHasCoverImageFile entity)
    {
        EnsureCoverLoaded(entity);

        var row = entity.CoverImageFile;
        return row != null && File.Exists(Path.Join(directoryService.CoverImageDirectory, row.FileName));
    }

    public async Task WriteBookmarkAsync(AppUserBookmark bookmark, string sourcePath, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new  ImagePolicy(settings.EncodeMediaAs, CoverImageSize.Default);
        var format = policy.Format.ForKind(ImageKind.Bookmark);

        var stem = BookmarkService.BookmarkStem(bookmark.AppUserId, bookmark.SeriesId, bookmark.ChapterId);
        var directory = Path.Join(await GetImageDirectoryAsync(ImageKind.Bookmark, ct), stem);
        var fileName = Path.GetFileNameWithoutExtension(sourcePath) + format.GetExtension();

        var written = Path.GetExtension(sourcePath).Equals(format.GetExtension(), StringComparison.OrdinalIgnoreCase)
            ? imagePipeline.Copy(sourcePath, ImageKind.Bookmark, directory, fileName)
            : imagePipeline.Write(new ImageSource.FromFile(sourcePath), ImageKind.Bookmark, policy, directory, fileName);


        var row = new ImageFile
        {
            Kind = ImageKind.Bookmark,
            FileName = Path.Join(stem, written.FileName),
            Origin = ImageOrigin.Scan,
            Format = format,
            Width = written.Width,
            Height = written.Height,
            SourceWidth = written.SourceWidth,
            SourceHeight = written.SourceHeight,
        };

        unitOfWork.ImageFileRepository.Add(row);
        bookmark.ImageFile = row;
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

        // Sharers are volumes/series that share a cover from a chapter/volume; a locked one is unshared so it keeps its cover
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
            logger.LogDebug("{Type} {Id} has no cover of its own to keep: {File} is missing, so it is unlocked and stays on the shared row",
                sharer.GetType().Name, sharer.Id, source.FileName);

            if (sharer is IHasLockableCoverImageFile lockable)
            {
                lockable.CoverImageLocked = false;
            }
            return;
        }

        var target = Describe(sharer);
        var fileName = target.Stem + Path.GetExtension(source.FileName);
        var existing = await unitOfWork.ImageFileRepository.GetByNameAsync(target.Kind, fileName, ct);

        var written = imagePipeline.Copy(sourcePath, target.Kind, directoryService.CoverImageDirectory, fileName);

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
    }

    private async Task ReleaseIfUnheldAsync(ImageFile? previous, List<string> staleFiles,
        CancellationToken ct)
    {
        if (previous == null) return;
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

    private async Task FinishAsync(bool commit, List<string> staleFiles, List<IHasCoverImageFile> changed,
        CancellationToken ct)
    {
        if (!commit)
        {
            _replacedFiles.AddRange(staleFiles);
            return;
        }

        await unitOfWork.CommitAsync(ct);

        foreach (var fileName in staleFiles)
        {
            directoryService.DeleteFileQuietly(Path.Join(directoryService.CoverImageDirectory, fileName));
        }

        foreach (var entity in changed)
        {
            await eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(entity.Id, Describe(entity).EventType), false, ct);
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


    private static CoverTarget Describe(IHasCoverImageFile entity) => entity switch
    {
        Chapter chapter => new CoverTarget(ImageKind.ChapterCover, ImageHelper.GetChapterFormat(chapter.Id, chapter.VolumeId),
            MessageFactoryEntityTypes.Chapter),
        Volume volume => new CoverTarget(ImageKind.VolumeCover, ImageHelper.GetVolumeFormat(volume.Id),
            MessageFactoryEntityTypes.Volume),
        Series series => new CoverTarget(ImageKind.SeriesCover, ImageHelper.GetSeriesFormat(series.Id),
            MessageFactoryEntityTypes.Series),
        Library library => new CoverTarget(ImageKind.LibraryIcon, ImageHelper.GetLibraryFormat(library.Id),
            MessageFactoryEntityTypes.Library),
        Person person => new CoverTarget(ImageKind.PersonCover, ImageHelper.GetPersonFormat(person.Id),
            MessageFactoryEntityTypes.Person),
        ReadingList readingList => new CoverTarget(ImageKind.ReadingListCover, ImageHelper.GetReadingListFormat(readingList.Id),
            MessageFactoryEntityTypes.ReadingList),
        AppUser user => new CoverTarget(ImageKind.UserCover, ImageHelper.GetUserFormat(user.Id),
            MessageFactoryEntityTypes.User),
        AppUserCollection collection => new CoverTarget(ImageKind.CollectionCover, ImageHelper.GetCollectionTagFormat(collection.Id),
            MessageFactoryEntityTypes.Collection),
        _ => throw new ArgumentOutOfRangeException(nameof(entity), entity.GetType().Name, null),
    };

    private readonly record struct CoverTarget(ImageKind Kind, string Stem, string EventType);
}
