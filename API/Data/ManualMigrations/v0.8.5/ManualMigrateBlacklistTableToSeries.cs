using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Entities.Metadata;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.5 - Migrating Kavita+ BlacklistedSeries table to Series entity to streamline implementation and generate a "Needs Manual Match" entry for the Series
/// </summary>
public static class ManualMigrateBlacklistTableToSeries
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateBlacklistTableToSeries"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateBlacklistTableToSeries migration - Please be patient, this may take some time. This is not an error");

        // Get all series in the Blacklist table and set their IsBlacklist = true
        var blacklistedSeries = await context.SeriesBlacklist
            .Include(s => s.Series.ExternalSeriesMetadata)
            .Select(s => s.Series)
            .ToListAsync();

        foreach (var series in blacklistedSeries)
        {
            series.IsBlacklisted = true;
            series.ExternalSeriesMetadata ??= new ExternalSeriesMetadata() { SeriesId = series.Id };

            if (series.ExternalSeriesMetadata.AniListId > 0)
            {
                series.IsBlacklisted = false;
                logger.LogInformation("{SeriesName} was in Blacklist table, but has valid AniList Id, not blacklisting", series.Name);
            }

            context.Series.Entry(series).State = EntityState.Modified;
        }
        // Remove everything in SeriesBlacklist (it will be removed in another migration)
        context.SeriesBlacklist.RemoveRange(context.SeriesBlacklist);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateBlacklistTableToSeries",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateBlacklistTableToSeries migration - Completed. This is not an error");
    }
}
