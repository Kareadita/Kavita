using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface IActiveUserTrackerService
{
    void RecordActive(int userId);
    Task FlushAsync(CancellationToken ct = default);
}
