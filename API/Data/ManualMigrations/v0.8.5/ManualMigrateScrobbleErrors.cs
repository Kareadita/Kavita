using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.5 - There seems to be some scrobble events that are pre-scrobble error table that can be processed over and over.
/// This will take the given year and minus 1 from it and clear everything from that and anything that is errored.
/// </summary>
public static class ManualMigrateScrobbleErrors
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateScrobbleErrors"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateScrobbleErrors migration - Please be patient, this may take some time. This is not an error");

        // Get all series in the Blacklist table and set their IsBlacklist = true
        var events = await context.ScrobbleEvent
            .Where(se => se.LastModifiedUtc <= DateTime.UtcNow.AddYears(-1) || se.IsErrored)
            .ToListAsync();

        context.ScrobbleEvent.RemoveRange(events);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Removed {Count} old scrobble events", events.Count);
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateScrobbleErrors",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateScrobbleErrors migration - Completed. This is not an error");
    }
}
