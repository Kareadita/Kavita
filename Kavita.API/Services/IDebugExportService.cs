using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface IDebugExportService
{
    /// <summary>
    /// Builds a scrubbed database, logs and system-info.json into one zip under <c>config/debug-exports/</c>,
    /// then sends <c>DebugExportReady</c> or <c>DebugExportFailed</c> to admins
    /// </summary>
    Task ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// The finished zip for <paramref name="exportId"/>, or null when the id is malformed or the export isn't finished
    /// </summary>
    string? GetZipPath(string exportId);
}
