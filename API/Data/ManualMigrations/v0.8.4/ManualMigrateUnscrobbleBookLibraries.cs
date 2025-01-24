using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// When I removed Scrobble support for Book libraries, I forgot to turn the setting off for said libraries.
/// </summary>
public static class ManualMigrateUnscrobbleBookLibraries
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateUnscrobbleBookLibraries"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateUnscrobbleBookLibraries migration - Please be patient, this may take some time. This is not an error");

        var libs = await context.Library.Where(l => l.Type == LibraryType.Book).ToListAsync();
        foreach (var lib in libs)
        {
            lib.AllowScrobbling = false;
            context.Entry(lib).State = EntityState.Modified;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateUnscrobbleBookLibraries",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateUnscrobbleBookLibraries migration - Completed. This is not an error");
    }
}
