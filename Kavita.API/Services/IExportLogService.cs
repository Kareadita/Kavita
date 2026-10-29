using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DebugExport;

namespace Kavita.API.Services;

public interface IExportLogService
{
    /// <summary>
    /// Streams the newest log files through <paramref name="scrubber"/> into <paramref name="zip"/>, one entry per file under <c>logs/</c>
    /// </summary>
    Task WriteLogsAsync(ZipArchive zip, ExportTextScrubber scrubber, CancellationToken ct = default);
}
