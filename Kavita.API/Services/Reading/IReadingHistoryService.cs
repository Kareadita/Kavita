using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Reading;

public interface IReadingHistoryService
{
    Task AggregateYesterdaysActivity(CancellationToken ct = default);
}
