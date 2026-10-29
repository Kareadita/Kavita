using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DebugExport;

namespace Kavita.API.Services;

public interface IExportDatabaseScrubService
{
    /// <summary>
    /// Replaces every <c>[ExportScrub]</c> value in the snapshot at <paramref name="snapshotPath"/> and adds one admin to it, in one transaction
    /// </summary>
    Task<ExportScrubResult> ScrubAsync(string snapshotPath, CancellationToken ct = default);
}
