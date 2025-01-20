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
/// Due to a bug in the initial merge of People/Scanner rework, people got messed up bad. This migration will clear out the table only for nightly users: 0.8.3.15/0.8.3.16
/// </summary>
public static class ManualMigrateRemovePeople
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateRemovePeople"))
        {
            return;
        }

        var version = BuildInfo.Version.ToString();
        if (version != "0.8.3.15" && version != "0.8.3.16")
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateRemovePeople migration - Please be patient, this may take some time. This is not an error");

        context.Person.RemoveRange(context.Person);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateRemovePeople",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateRemovePeople migration - Completed. This is not an error");
    }
}
