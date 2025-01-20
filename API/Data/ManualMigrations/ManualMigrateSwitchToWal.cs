using System;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.2 switches Default Kavita installs to WAL
/// </summary>
public static class ManualMigrateSwitchToWal
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateSwitchToWal"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateSwitchToWal migration - Please be patient, this may take some time. This is not an error");
        try
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting WAL");
            /* Swallow */
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateSwitchToWal",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateSwitchToWal migration - Completed. This is not an error");
    }

}
