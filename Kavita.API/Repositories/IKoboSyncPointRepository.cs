using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Kobo;

namespace Kavita.API.Repositories;

public interface IKoboSyncPointRepository
{
    Task<KoboSyncPoint> GetSyncPoint(int userId, int? syncPointId, CancellationToken cancellationToken);
}
