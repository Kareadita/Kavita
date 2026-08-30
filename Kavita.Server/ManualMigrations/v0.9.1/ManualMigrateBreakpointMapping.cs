using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1.x;

/// <summary>
/// v0.9.1 updated breakpoint service which streamlined the enum values to breakpoint pixels. The backend missed the mapping update. Just in case, update existing data.
/// </summary>
/// <remarks>This is for the v0.9.1 hotfix</remarks>
public class ManualMigrateBreakpointMapping : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateBreakpointMapping);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        await context.AppUserReadingProfiles
            .Where(p => (int) p.DisableWidthOverride == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DisableWidthOverride, BreakPoint.Mobile));

        await context.AppUserReadingProfiles
            .Where(p => (int) p.DisableWidthOverride == 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DisableWidthOverride, BreakPoint.Tablet));

        await context.AppUserReadingProfiles
            .Where(p => (int) p.DisableWidthOverride == 3)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DisableWidthOverride, BreakPoint.Desktop));
    }
}
