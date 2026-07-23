using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 restored the ability to rename a Series' Name from the UI (and lets Kavita+ write it).
/// OriginalName is the on-disk anchor used to re-find a series after its Name changes. Historically
/// OriginalName could be null, which would make a renamed series look brand-new on the next scan
/// (the old one getting removed and cascading its data). Backfill any null/empty OriginalName from
/// the current Name so every existing series is safely renameable.
/// </summary>
public class ManualMigrateOriginalNameBackfill : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateOriginalNameBackfill);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var series = await context.Series
            .Where(s => s.OriginalName == null || s.OriginalName == string.Empty)
            .ToListAsync();
        if (series.Count == 0) return;

        foreach (var s in series)
        {
            s.OriginalName = s.Name;
        }

        await context.SaveChangesAsync();
    }
}
