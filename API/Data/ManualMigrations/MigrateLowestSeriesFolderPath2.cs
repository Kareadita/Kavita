using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.3 still had a bug around LowestSeriesPath. This resets it for all users.
/// </summary>
public static class MigrateLowestSeriesFolderPath2
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateLowestSeriesFolderPath2"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateLowestSeriesFolderPath2 migration - Please be patient, this may take some time. This is not an error");

        var series = await dataContext.Series.Where(s => !string.IsNullOrEmpty(s.LowestFolderPath)).ToListAsync();
        foreach (var s in series)
        {
            s.LowestFolderPath = string.Empty;
            unitOfWork.SeriesRepository.Update(s);
        }

        // Save changes after processing all series
        if (dataContext.ChangeTracker.HasChanges())
        {
            await dataContext.SaveChangesAsync();
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateLowestSeriesFolderPath2",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running MigrateLowestSeriesFolderPath2 migration - Completed. This is not an error");
    }
}
