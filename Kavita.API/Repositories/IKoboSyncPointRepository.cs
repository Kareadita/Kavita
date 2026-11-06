using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Kobo;

namespace Kavita.API.Repositories;

public interface IKoboSyncPointRepository
{
    /// <summary>
    /// Get or create a new SyncPoint. Does not commit
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="syncPointId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<KoboSyncPoint> GetSyncPoint(int userId, int? syncPointId, CancellationToken cancellationToken);
}
