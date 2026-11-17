using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// v0.9.2 - Queues <see cref="ICleanupService.FinishImageFileMigration"/>, which brings the files behind the backfilled
/// image rows in line with the settings and writes this migration's history row when it finishes.
/// </summary>
/// <remarks>
/// Not a <see cref="ManualMigration"/> as we need to be able to run the job async, since <see cref="ManualMigration"/> saves the History row after Execute.
/// </remarks>
public static class ManualMigrateImageFileBackfillPart2
{
    public static async Task Migrate(DataContext context, IBackgroundJobClient backgroundJobClient, ILogger logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == CleanupService.ImageFileMigrationName))
        {
            return;
        }

        logger.LogCritical("Queueing ManualMigrateImageFileBackfillPart2 - Images are checked and fixed in the background. This is not an error");

        backgroundJobClient.Enqueue<ICleanupService>(s => s.FinishImageFileMigration(CancellationToken.None));
    }
}
