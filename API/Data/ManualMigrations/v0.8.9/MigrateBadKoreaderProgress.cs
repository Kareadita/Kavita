using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.8.28 - There was bad code in the nightlies where Progress events with LibraryId = 0 could be saved. This will fix up those events.
/// </summary>
public class MigrateBadKoreaderProgress : ManualMigration
{
    protected override string MigrationName => "MigrateBadKoreaderProgress";

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var badProgressWithLibrary = await context.AppUserProgresses
            .Where(p => p.LibraryId == 0)
            .Join(
                context.Series,
                progress => progress.SeriesId,
                series => series.Id,
                (progress, series) => new { Progress = progress, series.LibraryId })
            .ToListAsync();

        if (badProgressWithLibrary.Count == 0) return;

        logger.LogInformation("Found {Count} progress records with LibraryId = 0", badProgressWithLibrary.Count);

        foreach (var item in badProgressWithLibrary)
        {
            item.Progress.LibraryId = item.LibraryId;
        }

        await context.SaveChangesAsync();

        logger.LogInformation("Successfully fixed {Count} progress records", badProgressWithLibrary.Count);
    }
}
