using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Plus;

public interface IWantToReadSyncService
{
    Task Sync(CancellationToken ct = default);
}
