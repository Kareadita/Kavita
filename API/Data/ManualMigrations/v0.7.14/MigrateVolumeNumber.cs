using System.Threading.Tasks;
using API.Entities.History;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Introduced in v0.7.14, this migrates the existing Volume Name -> Volume Min/Max Number
/// </summary>
public static class MigrateVolumeNumber
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateVolumeNumber"))
        {
            return;
        }

        if (await dataContext.Volume.AnyAsync(v => v.MaxNumber > 0))
        {
            logger.LogCritical(
                "Running MigrateVolumeNumber migration - Completed. This is not an error");
            await SaveManualHistoryItem(dataContext);
            return;
        }

        logger.LogCritical(
            "Running MigrateVolumeNumber migration - Please be patient, this may take some time. This is not an error");

        // Get all volumes
        foreach (var volume in dataContext.Volume)
        {
            volume.MinNumber = Parser.MinNumberFromRange(volume.Name);
            volume.MaxNumber = Parser.MaxNumberFromRange(volume.Name);
        }

        await dataContext.SaveChangesAsync();
        await SaveManualHistoryItem(dataContext);

        logger.LogCritical("Running MigrateVolumeNumber migration - Completed. This is not an error");
    }

    private static async Task SaveManualHistoryItem(DataContext context)
    {
        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = nameof(MigrateVolumeNumber),
        });
        await context.SaveChangesAsync();
    }
}
