using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;
#nullable enable

/// <summary>
/// Some linux-based users are having non-rooted LowestFolderPaths. This will attempt to fix it or null them.
/// Fixed in v0.8.2
/// </summary>
public static class MigrateSeriesLowestFolderPath
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger, IDirectoryService directoryService)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateSeriesLowestFolderPath"))
        {
            return;
        }

        logger.LogCritical("Running MigrateSeriesLowestFolderPath migration - Please be patient, this may take some time. This is not an error");

        var seriesWithFolderPath =
            await dataContext.Series.Where(s => !string.IsNullOrEmpty(s.LowestFolderPath))
                .Include(s => s.Library)
                .ThenInclude(l => l.Folders)
                .ToListAsync();

        foreach (var series in seriesWithFolderPath)
        {
            var isValidPath = series.Library.Folders
                .Any(folder => Parser.NormalizePath(series.LowestFolderPath!).StartsWith(Parser.NormalizePath(folder.Path), StringComparison.OrdinalIgnoreCase));

            if (isValidPath) continue;
            series.LowestFolderPath = null;
            dataContext.Entry(series).State = EntityState.Modified;
        }

        await dataContext.SaveChangesAsync();



        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateSeriesLowestFolderPath",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();

        logger.LogCritical("Running MigrateSeriesLowestFolderPath migration - Completed. This is not an error");
    }
}
