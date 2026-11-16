using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
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
    /// Tracked chapters, volumes and series whose cover is <paramref name="imageFileId"/>
    /// </summary>
    Task<ImageFileHolders> GetHoldersAsync(int imageFileId, CancellationToken ct = default);
    /// <summary>
    /// Get every <see cref="ImageOrigin"/> that belongs to a Backup, which Kavita cannot rebuild. Bookmarks are excluded as they
    /// go into a separate backup path.
    /// </summary>
    /// <remarks>As NoTracking()</remarks>
    Task<IList<ImageFile>> GetForBackupAsync(CancellationToken ct = default);
}
