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
/// v0.8.0 ensured that MangaFile Path is normalized. This will normalize existing data to avoid churn.
/// </summary>
public static class MigrateDuplicateDarkTheme
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateDuplicateDarkTheme"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateDuplicateDarkTheme migration - Please be patient, this may take some time. This is not an error");

        var darkThemes = await dataContext.SiteTheme.Where(t => t.Name == "Dark").ToListAsync();

        if (darkThemes.Count > 1)
        {
            var correctDarkTheme = darkThemes.First(d => !string.IsNullOrEmpty(d.Description));

            // Get users
            var users = await dataContext.AppUser
                .Include(u => u.UserPreferences)
                .ThenInclude(p => p.Theme)
                .Where(u => u.UserPreferences.Theme.Name == "Dark")
                .ToListAsync();

            // Find any users that have a duplicate Dark theme as default and switch to the correct one
            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.UserPreferences.Theme.Description))
                {
                    user.UserPreferences.Theme = correctDarkTheme;
                }
            }
            await dataContext.SaveChangesAsync();

            // Now remove the bad themes
            dataContext.SiteTheme.RemoveRange(darkThemes.Where(d => string.IsNullOrEmpty(d.Description)));

            await dataContext.SaveChangesAsync();
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateDuplicateDarkTheme",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateDuplicateDarkTheme migration - Completed. This is not an error");
    }
}
