using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;

namespace Kavita.API.Repositories;

public interface IImageFileRepository
{
    void Add(ImageFile file);
    void Remove(ImageFile file);
    Task<ImageFile?> GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>
    /// The row for <paramref name="fileName"/> in the folder the <paramref name="kind"/> is stored in.
    /// A name is only unique within a kind
    /// </summary>
    Task<ImageFile?> GetByNameAsync(ImageKind kind, string fileName, CancellationToken ct = default);
    /// <summary>
    /// The favicon or publisher row for <paramref name="key"/> (a host or a publisher name)
    /// </summary>
    Task<ImageFile?> GetByKeyAsync(ImageKind kind, string key, CancellationToken ct = default);
    /// <summary>
    /// Tracked chapters, volumes and series whose cover is <paramref name="imageFileId"/>
    /// </summary>
    Task<ImageFileHolders> GetHoldersAsync(int imageFileId, CancellationToken ct = default);
    /// <summary>
    /// Get every <see cref="ImageOrigin"/> that belongs to a Backup, which Kavita cannot rebuild. Bookmarks are excluded as they
    /// go into a separate backup path.
    /// </summary>
    /// <remarks>As NoTracking()</remarks>
    Task<IList<ImageFile>> GetForBackupAsync(CancellationToken ct = default);
    /// <summary>
    /// Tracked rows with no entities still on disk for physical file deletion.
    /// Favicon, publisher and bookmark rows are never returned.
    /// </summary>
    /// <remarks>Safe to remove each batch before reading the next</remarks>
    IAsyncEnumerable<List<ImageFile>> GetImageFilesForDeletionAsync(int batchSize, CancellationToken ct = default);
    /// <inheritdoc cref="GetImageFilesForDeletionAsync"/>
    Task<int> CountImageFilesForDeletionAsync(CancellationToken ct = default);
    /// <summary>
    /// Tracked rows of <paramref name="kind"/> with a known format that is not <paramref name="format"/>, or that are
    /// larger than <paramref name="box"/>
    /// </summary>
    /// <remarks>Safe to update each batch before reading the next</remarks>
    IAsyncEnumerable<List<ImageFile>> GetImageFilesForReencodeAsync(ImageKind kind, EncodeFormat format, ImageBox? box,
        int batchSize, CancellationToken ct = default);
    /// <inheritdoc cref="GetImageFilesForReencodeAsync"/>
    Task<int> CountImageFilesForReencodeAsync(ImageKind kind, EncodeFormat format, ImageBox? box, CancellationToken ct = default);
    /// <summary>
    /// Tracked entities of every kind whose cover is one of <paramref name="imageFileIds"/>
    /// </summary>
    Task<IList<IHasCoverImageFile>> GetAllHoldersAsync(IList<int> imageFileIds, CancellationToken ct = default);

    #region Migration Only

    /// <summary>
    /// Whether any row still has no <see cref="ImageFile.Format"/>, as the v0.9.2 backfill left it
    /// </summary>
    Task<bool> HasImageFilesNeedingMigrationAsync(CancellationToken ct = default);
    /// <summary>
    /// Rows with no <see cref="ImageFile.Format"/>, as the v0.9.2 backfill left them
    /// </summary>
    Task<int> CountImageFilesNeedingMigrationAsync(CancellationToken ct = default);
    /// <summary>
    /// Tracked rows of <paramref name="kind"/> with no <see cref="ImageFile.Format"/>, as the v0.9.2 backfill left them
    /// </summary>
    /// <remarks>Safe to update each batch before reading the next</remarks>
    IAsyncEnumerable<List<ImageFile>> GetImageFilesNeedingMigrationAsync(ImageKind kind, int batchSize, CancellationToken ct = default);
    /// <summary>
    /// File names of every row of the given kinds
    /// </summary>
    Task<IList<string>> GetFileNamesAsync(IList<ImageKind> kinds, CancellationToken ct = default);
    /// <summary>
    /// Tracked favicon and publisher rows whose <see cref="ImageFile.Key"/> another row of the same kind also has, as the
    /// backfill left them when an old format change had left both files on disk
    /// </summary>
    Task<IList<ImageFile>> GetKeyedImageFilesWithDuplicateKeysAsync(CancellationToken ct = default);

    #endregion
}
