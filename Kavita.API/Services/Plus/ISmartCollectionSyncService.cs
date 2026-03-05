using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Plus;

/// <summary>
/// Responsible to synchronize Collection series from non-Kavita sources
/// </summary>
public interface ISmartCollectionSyncService
{
    /// <summary>
    /// Synchronize all collections
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Sync(CancellationToken ct = default);

    /// <summary>
    /// Synchronize a collection
    /// </summary>
    /// <param name="collectionId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Sync(int collectionId, CancellationToken ct = default);
}
