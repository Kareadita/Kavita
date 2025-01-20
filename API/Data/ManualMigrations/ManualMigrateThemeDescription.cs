using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.2 introduced Theme repo viewer, this adds Description to existing SiteTheme defaults
/// </summary>
public static class ManualMigrateThemeDescription
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateThemeDescription"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateThemeDescription migration - Please be patient, this may take some time. This is not an error");

        var theme = await context.SiteTheme.FirstOrDefaultAsync(t => t.Name == "Dark");
        if (theme != null)
        {
            theme.Description = Seed.DefaultThemes.First().Description;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }




        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateThemeDescription",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateThemeDescription migration - Completed. This is not an error");
    }
}
