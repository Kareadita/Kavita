using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;
using Kavita.Models.Images;

namespace Kavita.API.Services;


public interface IImageFileService
{
    /// <remarks>
    /// With commit true (the default) a CoverUpdate is sent for every entity whose image changed, after saving.
    /// With commit false nothing is sent, so the caller sends the events after its own commit
    /// </remarks>
    Task SetCoverAsync(IHasCoverImageFile entity, ImageSource source, CoverWrite write, bool commit = true,
        CancellationToken ct = default);

    /// <remarks>
    /// With commit true (the default) a CoverUpdate is sent for every entity whose image changed, after saving.
    /// With commit false nothing is sent, so the caller sends the events after its own commit
    /// </remarks>
    Task ShareCoverAsync(IHasCoverImageFile target, IHasCoverImageFile source, bool lockBoth, bool commit = true,
        CancellationToken ct = default);

    Task ClearCoverAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default);

    /// <summary>
    /// Recomputes the ColorScape of the entity's cover row from its file. Does nothing when there is no row or no file
    /// </summary>
    Task RefreshColorsAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default);

    /// <summary>
    /// Deletes the files that writes with <c>commit: false</c> in this scope replaced. Call it after your own commit
    /// </summary>
    void DeleteReplacedFiles();
    Task UpdateLockAsync(IHasLockableCoverImageFile entity, bool locked, bool commit = true, CancellationToken ct = default);

    /// <summary>
    /// Writes a <see cref="ImageKind.Favicon"/> or <see cref="ImageKind.Publisher"/> image, which no entity points at,
    /// into its row looked up by kind and file name. Commits
    /// </summary>
    /// <param name="key">The host for a favicon, the name for a publisher</param>
    /// <param name="fileName">Name with the extension, relative to the kind's folder</param>
    /// <returns>The file name written</returns>
    Task<string> WriteKeyedAsync(ImageSource source, ImageKind kind, string key, string fileName, ImageOrigin origin,
        CancellationToken ct = default);

    /// <summary>
    /// The folder images of <paramref name="kind"/> are stored in
    /// </summary>
    Task<string> GetImageDirectoryAsync(ImageKind kind, CancellationToken ct = default);

    /// <summary>
    /// True when the entity has a cover row and its file is on disk
    /// </summary>
    bool HasCoverFile(IHasCoverImageFile entity);

    /// <summary>
    /// Writes the page into the bookmark folder in the server's encode format, adds its row and links it to the bookmark.
    /// Does not commit
    /// </summary>
    /// <param name="sourcePath">Full path of the cached page</param>
    Task WriteBookmarkAsync(AppUserBookmark bookmark, string sourcePath, CancellationToken ct = default);
}
