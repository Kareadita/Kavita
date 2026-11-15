using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Images;

namespace Kavita.API.Services;

public interface ICompositeCoverService
{
    /// <summary>
    /// Builds a reading list or collection cover from its item covers. Does nothing when the cover is locked or was not
    /// built by Kavita, and skips the build when the item covers are the same as last time
    /// </summary>
    /// <param name="kind"><see cref="ImageKind.ReadingListCover"/> or <see cref="ImageKind.CollectionCover"/></param>
    /// <param name="force">Build even when the item covers are unchanged</param>
    Task RefreshAsync(ImageKind kind, int id, bool force, CancellationToken ct = default);
}
