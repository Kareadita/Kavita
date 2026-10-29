using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.DebugExport;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.DebugExport;

public class ExportDatabaseScrubService(
    ILogger<ExportDatabaseScrubService> logger,
    IAccountService accountService)
    : IExportDatabaseScrubService
{
    public Task<ExportScrubResult> ScrubAsync(string snapshotPath, CancellationToken ct = default)
    {
        return Task.FromResult(new ExportScrubResult(new ExportJunk(), new Dictionary<string, string>(),
            string.Empty, string.Empty, string.Empty));
    }
}
