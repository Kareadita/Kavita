using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DebugExport;

namespace Kavita.API.Services;

public interface IExportSystemInfoService
{
    /// <summary>
    /// Reads the server's runtime, mounts, K+ state and disk timings. Hosts go through <paramref name="result"/>'s junk, so they match the scrubbed database
    /// </summary>
    /// <param name="exportFolder">The export's own folder under <c>config/</c>, where the write timing's temp file goes</param>
    Task<ExportSystemInfo> BuildAsync(ExportScrubResult result, string exportFolder, CancellationToken ct = default);
}
