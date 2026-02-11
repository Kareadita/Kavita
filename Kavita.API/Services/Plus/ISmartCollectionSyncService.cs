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
    /// <returns></returns>
    Task Sync();
    /// <summary>
    /// Synchronize a collection
    /// </summary>
    /// <param name="collectionId"></param>
    /// <returns></returns>
    Task Sync(int collectionId);
}
