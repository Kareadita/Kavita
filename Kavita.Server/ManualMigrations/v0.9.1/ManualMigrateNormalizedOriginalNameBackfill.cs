using System.Linq;
using System.Threading.Tasks;
using Kavita.Common.Extensions;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 added a stored NormalizedOriginalName column so folder-to-series matching and rename
/// uniqueness checks can run entirely DB-side. Backfill it from OriginalName for all existing rows.
/// Must run AFTER <see cref="ManualMigrateOriginalNameBackfill"/>, which ensures OriginalName is populated.
/// </summary>
public class ManualMigrateNormalizedOriginalNameBackfill : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateNormalizedOriginalNameBackfill);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var series = await context.Series
            .Where(s => s.NormalizedOriginalName == null || s.NormalizedOriginalName == string.Empty)
            .ToListAsync();
        if (series.Count == 0) return;

        foreach (var s in series)
        {
            s.NormalizedOriginalName = (s.OriginalName ?? string.Empty).ToNormalized();
        }

        await context.SaveChangesAsync();
    }
}
