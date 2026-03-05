using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace Kavita.API.Services.Metadata;

public interface IWordCountAnalyzerService
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibrary(int libraryId, bool forceUpdate = false, CancellationToken ct = default);
    Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = true, CancellationToken ct = default);
}
