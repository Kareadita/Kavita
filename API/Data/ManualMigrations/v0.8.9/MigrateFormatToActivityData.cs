using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.8.16 - Needed to add Format to the ActivityData to optimize a query
/// </summary>
public class MigrateFormatToActivityDataV2 : ManualMigration
{
    protected override string MigrationName => nameof(MigrateFormatToActivityDataV2);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var activitiesWithoutFormat = await context.AppUserReadingSessionActivityData
            .Where(d => d.Format == MangaFormat.Unknown || d.Format == 0)
            .Select(d => d.ChapterId)
            .Distinct()
            .ToListAsync();

        if (activitiesWithoutFormat.Count == 0)
        {
            logger.LogInformation("No activity data requires Format backfill");
            return;
        }

        logger.LogInformation("Backfilling Format for {Count} chapters worth of activity data", activitiesWithoutFormat.Count);

        // Batch fetch formats for all affected chapters
        var chapterFormats = await context.MangaFile
            .Where(f => activitiesWithoutFormat.Contains(f.ChapterId))
            .GroupBy(f => f.ChapterId)
            .Select(g => new
            {
                ChapterId = g.Key,
                Format = g.OrderBy(f => f.Id).First().Format
            })
            .ToDictionaryAsync(x => x.ChapterId, x => x.Format);

        // Update in batches to avoid memory issues
        const int batchSize = 1000;
        var updated = 0;

        foreach (var chapterIdBatch in activitiesWithoutFormat.Chunk(batchSize))
        {
            var activities = await context.AppUserReadingSessionActivityData
                .Where(d => (d.Format == MangaFormat.Unknown || d.Format == 0) && chapterIdBatch.Contains(d.ChapterId))
                .ToListAsync();

            foreach (var activity in activities)
            {
                if (!chapterFormats.TryGetValue(activity.ChapterId, out var format)) continue;

                activity.Format = format;
                updated++;
            }

            await context.SaveChangesAsync();
        }

        logger.LogInformation("Backfilled Format for {Count} activity records", updated);
    }
}
