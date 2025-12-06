using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Entities.History;
using API.Entities.Progress;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public class MigrateTotalReads : ManualMigration
{
    private const int BatchSize = 1000;

    protected override string MigrationName => nameof(MigrateTotalReads);

    protected override async Task ExecuteAsync(DataContext dataContext, ILogger<Program> logger)
    {
        var totalProgressRecords = await dataContext.AppUserProgresses.CountAsync();
        if (totalProgressRecords > 0)
        {
            logger.LogInformation("Found {Count} progress records to migrate", totalProgressRecords);

            var totalBatches = (int)Math.Ceiling(totalProgressRecords / (double)BatchSize);
            var migratedCount = 0;

            for (var batchNumber = 0; batchNumber < totalBatches; batchNumber++)
            {
                // Join with Chapter to get TotalPages and WordCount
                var progressBatch = await dataContext.AppUserProgresses
                    .AsNoTracking()
                    .Where(p => p.PagesRead > 0)
                    .OrderBy(p => p.Id)
                    .Skip(batchNumber * BatchSize)
                    .Take(BatchSize)
                    .Join(dataContext.Chapter,
                        p => p.ChapterId,
                        c => c.Id,
                        (progress, chapter) => new { Progress = progress, Chapter = chapter })
                    .Join(dataContext.Series,
                        p => p.Progress.SeriesId,
                        s => s.Id,
                        (combo, series) => new {combo.Progress, combo.Chapter, series.Format })
                    .Where(d => d.Progress.PagesRead >= d.Chapter.Pages)
                    .ToListAsync();

                foreach (var progress in progressBatch)
                {
                    progress.Progress.TotalReads = 1;
                    dataContext.AppUserProgresses.Update(progress.Progress);
                    migratedCount += 1;
                }

                if (dataContext.ChangeTracker.HasChanges())
                {
                    await dataContext.SaveChangesAsync();
                }

                logger.LogInformation("Migrated batch {Current}/{Total} ({Count} with TotalReads)",
                    batchNumber + 1, totalBatches, migratedCount);
            }

            logger.LogInformation("Migration complete: {Count}/{Total} progress records updated with total reads",
                migratedCount, totalProgressRecords);
        }
        else
        {
            logger.LogInformation("No progress records found to migrate");
        }
    }
}
