using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Models.Entities.History;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations;

/// <summary>
/// Runs a Migration before/after EF Core
/// </summary>
/// <remarks>Update .editorconfig to suppress warnings once a minor/major release bump has occured (v0.8 -> v0.9)</remarks>
public abstract class ManualMigration
{
    protected abstract string MigrationName { get; }

    /// <summary>
    /// Execute the migration logic. Handle your own exceptions.
    /// </summary>
    protected abstract Task ExecuteAsync(DataContext context, ILogger logger);


    public async Task RunAsync(DataContext context, ILogger logger)
    {
        // Ensure we clear the ChangeTracker between migration runs, so we are starting fresh
        context.ChangeTracker.Clear();

        // Check if already run
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == MigrationName))
        {
            return;
        }
        logger.LogCritical("Running {MigrationName} migration - Please be patient, this may take some time. This is not an error", MigrationName);

        // Execute the migration
        await ExecuteAsync(context, logger);

        // Save history (only if no exception was thrown)
        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory
        {
            Name = MigrationName
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running {MigrationName} migration - Completed. This is not an error", MigrationName);
    }
}
