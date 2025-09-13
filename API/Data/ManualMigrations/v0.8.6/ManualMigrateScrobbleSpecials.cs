using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.History;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.6 - Change to not scrobble specials as they will never process, this migration removes all existing scrobble events
/// </summary>
public static class ManualMigrateScrobbleSpecials
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateScrobbleSpecials"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateScrobbleSpecials migration - Please be patient, this may take some time. This is not an error");

        // Get all series in the Blacklist table and set their IsBlacklist = true
        var events = await context.ScrobbleEvent
            .Where(se => se.VolumeNumber == Parser.SpecialVolumeNumber)
            .ToListAsync();

        context.ScrobbleEvent.RemoveRange(events);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Removed {Count} scrobble events that were specials", events.Count);
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateScrobbleSpecials",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateScrobbleSpecials migration - Completed. This is not an error");
    }
}
