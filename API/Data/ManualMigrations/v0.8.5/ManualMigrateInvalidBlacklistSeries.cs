using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.History;
using API.Entities.Metadata;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.5 - Migrating Kavita+ Series that are Blacklisted but have valid ExternalSeries row
/// </summary>
public static class ManualMigrateInvalidBlacklistSeries
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateInvalidBlacklistSeries"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateInvalidBlacklistSeries migration - Please be patient, this may take some time. This is not an error");

        // Get all series in the Blacklist table and set their IsBlacklist = true
        var blacklistedSeries = await context.Series
            .Include(s => s.ExternalSeriesMetadata)
            .Where(s => s.IsBlacklisted && s.ExternalSeriesMetadata.ValidUntilUtc > DateTime.MinValue)
            .ToListAsync();
        foreach (var series in blacklistedSeries)
        {
            series.IsBlacklisted = false;
            context.Series.Entry(series).State = EntityState.Modified;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateInvalidBlacklistSeries",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateInvalidBlacklistSeries migration - Completed. This is not an error");
    }
}
