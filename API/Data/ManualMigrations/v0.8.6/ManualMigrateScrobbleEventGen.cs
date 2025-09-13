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
/// v0.8.6 - Manually check when a user triggers scrobble event generation
/// </summary>
public static class ManualMigrateScrobbleEventGen
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateScrobbleEventGen"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateScrobbleEventGen migration - Please be patient, this may take some time. This is not an error");

        var users = await context.Users
            .Where(u => u.AniListAccessToken != null)
            .ToListAsync();

        foreach (var user in users)
        {
            if (await context.ScrobbleEvent.AnyAsync(se => se.AppUserId == user.Id))
            {
                user.HasRunScrobbleEventGeneration = true;
                user.ScrobbleEventGenerationRan = DateTime.UtcNow;
                context.AppUser.Update(user);
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateScrobbleEventGen",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateScrobbleEventGen migration - Completed. This is not an error");
    }
}
