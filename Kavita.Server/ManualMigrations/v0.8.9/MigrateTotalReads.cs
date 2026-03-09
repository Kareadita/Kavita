using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._8._9;

/// <summary>
/// v0.8.9 - This migrates all records that
/// </summary>
public class MigrateTotalReads : ManualMigration
{
    protected override string MigrationName => nameof(MigrateTotalReads);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var qualifyingProgressIds = await context.AppUserProgresses
            .AsNoTracking()
            .Where(p => p.PagesRead > 0)
            .Join(context.Chapter,
                p => p.ChapterId,
                c => c.Id,
                (progress, chapter) => new { progress.Id, progress.PagesRead, ChapterPages = chapter.Pages })
            .Where(x => x.PagesRead >= x.ChapterPages)
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync();

        if (qualifyingProgressIds.Count == 0)
        {
            logger.LogInformation("[{Scope}] No progress records found to migrate", MigrationName);
            return;
        }

        logger.LogInformation("[{Scope}] Found {Count} progress records to update with TotalReads",
            MigrationName, qualifyingProgressIds.Count);

        var updatedCount = await context.AppUserProgresses
            .Where(p => qualifyingProgressIds.Contains(p.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.TotalReads, 1));

        logger.LogInformation("[{Scope}] Migration complete: {Count} progress records updated",
            MigrationName, updatedCount);
    }
}
