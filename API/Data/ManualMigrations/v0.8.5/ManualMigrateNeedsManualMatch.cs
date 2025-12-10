using System;
using System.Threading.Tasks;
using API.DTOs.KavitaPlus.Manage;
using API.Entities.History;
using API.Extensions.QueryExtensions;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.5 - After user testing, the needs manual match has some edge cases from migrations and for best user experience,
/// should be reset to allow the upgraded system to process better.
/// </summary>
public static class ManualMigrateNeedsManualMatch
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateNeedsManualMatch"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateNeedsManualMatch migration - Please be patient, this may take some time. This is not an error");

        // Get all series in the Blacklist table and set their IsBlacklist = true
        var series = await context.Series
            .FilterMatchState(MatchStateOption.Error)
            .ToListAsync();

        foreach (var seriesEntry in series)
        {
            seriesEntry.IsBlacklisted = false;
            context.Series.Update(seriesEntry);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateNeedsManualMatch",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateNeedsManualMatch migration - Completed. This is not an error");
    }
}
