using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// Accounts created during the first nightly releases of 0.9.2 are in a broken state due to a missing migration
/// </summary>
public class ManualMigrationFixInvalidOnDeckSettings: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationFixInvalidOnDeckSettings);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        await context.AppUserPreferences
            .Where(p => p.OnDeckProgressDays == null || p.OnDeckProgressDays == 0)
            .ExecuteUpdateAsync(s
                => s.SetProperty(p => p.OnDeckProgressDays, 30));

        await context.AppUserPreferences
            .Where(p => p.OnDeckUpdateDays == null || p.OnDeckUpdateDays == 0)
            .ExecuteUpdateAsync(s
                => s.SetProperty(p => p.OnDeckUpdateDays, 7));
    }
}
