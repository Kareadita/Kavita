using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services.Metadata;

public interface IWordCountAnalyzerService
{
    Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = true, CancellationToken ct = default);
}
