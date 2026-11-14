using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Kobo;

namespace Kavita.API.Repositories;

public interface IKoboSyncPointRepository
{
    void Update(KoboSyncPoint syncPoint);

    /// <summary>
    /// Get or create a new SyncPoint. Does not commit
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="syncPointId"></param>
    /// <param name="koboStoreSyncToken"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<KoboSyncPoint> GetSyncPoint(int userId, int? syncPointId, string? koboStoreSyncToken, CancellationToken cancellationToken);
    Task<KoboSyncPoint?> GetSyncPoint(int userId, string koboDeviceId, CancellationToken cancellationToken);
    Task<KoboSyncPoint?> GetSyncPoint(int userId, int syncPointId, CancellationToken cancellationToken);
    Task<List<KoboSyncPointDto>> GetSyncPoints(int userId, CancellationToken cancellationToken);
    Task DeleteOthersForDeviceId(int syncPointId, string koboDeviceId, CancellationToken cancellationToken);
    Task DeleteSyncPoint(int userId, int syncPointId, CancellationToken cancellationToken);
}
