using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Database.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

public class ManualMigrationBackfillKoboAnnotationIds : ManualMigration
{
    private const int BatchSize = 1000;

    protected override string MigrationName => nameof(ManualMigrationBackfillKoboAnnotationIds);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var updated = 0;

        await foreach (var batch in context.AppUserAnnotation
                           .OrderBy(a => a.Id)
                           .BatchToAsyncEnumerable(BatchSize))
        {
            foreach (var annotation in batch.Where(a => string.IsNullOrEmpty(a.KoboId)))
            {
                annotation.KoboId = Guid.NewGuid().ToString();
                updated++;
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        logger.LogInformation("Backfilled Kobo annotation ids on {Count} annotations", updated);
    }
}
