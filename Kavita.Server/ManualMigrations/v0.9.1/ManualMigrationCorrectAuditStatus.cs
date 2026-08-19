using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// Audit Log entries with status = 2 and ErrorMessage = series/chapter-missing-required-ids need to be updated to status = 1 (Failure)
/// </summary>
public class ManualMigrationCorrectAuditStatus : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationCorrectAuditStatus);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        await context.KavitaPlusAuditLogs
            .Where(s => s.Status == AuditStatus.Info && (s.ErrorMessage == "series-missing-required-ids" ||
                                                         s.ErrorMessage == "chapter-missing-required-ids"))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AuditStatus.Failure));
    }
}
