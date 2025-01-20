using System;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Introduced in v0.7.14, will store history so that going forward, migrations can just check against the history
/// and I don't need to remove old migrations
/// </summary>
public static class MigrateManualHistory
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync())
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateManualHistory migration - Please be patient, this may take some time. This is not an error");

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateUserLibrarySideNavStream",
            ProductVersion = "0.7.9.0",
            RanAt = DateTime.UtcNow
        });

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateSmartFilterEncoding",
            ProductVersion = "0.7.11.0",
            RanAt = DateTime.UtcNow
        });
        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateLibrariesToHaveAllFileTypes",
            ProductVersion = "0.7.11.0",
            RanAt = DateTime.UtcNow
        });

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateEmailTemplates",
            ProductVersion = "0.7.14.0",
            RanAt = DateTime.UtcNow
        });
        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateVolumeNumber",
            ProductVersion = "0.7.14.0",
            RanAt = DateTime.UtcNow
        });

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateWantToReadExport",
            ProductVersion = "0.7.14.0",
            RanAt = DateTime.UtcNow
        });

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateWantToReadImport",
            ProductVersion = "0.7.14.0",
            RanAt = DateTime.UtcNow
        });

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateManualHistory",
            ProductVersion = "0.7.14.0",
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateManualHistory migration - Completed. This is not an error");
    }
}
