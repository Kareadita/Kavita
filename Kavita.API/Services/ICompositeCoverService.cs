using System.Threading;
using System.Threading.Tasks;
using Hangfire;
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
    /// <summary>
    /// Builds the cover only when the reading list or collection has no cover yet and is not locked
    /// </summary>
    /// <param name="kind"><see cref="ImageKind.ReadingListCover"/> or <see cref="ImageKind.CollectionCover"/></param>
    Task CreateIfMissingAsync(ImageKind kind, int id, CancellationToken ct = default);
    /// <summary>
    /// <see cref="CreateIfMissingAsync"/> for every reading list and collection that has item covers but no cover of its own
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60), AutomaticRetry(Attempts = 0)]
    Task CreateMissingCoversAsync(CancellationToken ct = default);
    /// <summary>
    /// <see cref="CreateMissingCoversAsync"/>, plus <see cref="RefreshAsync"/> on every cover Kavita built, so item changes
    /// since the last run are picked up and a list or collection left with no item covers loses its cover
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60), AutomaticRetry(Attempts = 0)]
    Task RefreshAllAsync(CancellationToken ct = default);
}
