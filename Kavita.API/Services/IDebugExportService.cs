using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace Kavita.API.Services;

public interface IDebugExportService
{
    // Hangfire reads filters from the type a job is queued on, and ServerController queues this on the interface
    /// <summary>
    /// Builds a scrubbed database, logs and system-info.json into one zip under <c>config/debug-exports/</c>,
    /// then sends <c>DebugExportReady</c> or <c>DebugExportFailed</c> to admins
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    Task ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// The finished zip for <paramref name="exportId"/>, or null when the id is malformed or the export isn't finished
    /// </summary>
    string? GetZipPath(string exportId);
}
