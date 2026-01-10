using System.Threading.Tasks;
using API.Entities.History;
using API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.Misc;

public abstract class ManualMigration
{
    protected abstract string MigrationName { get; }

    /// <summary>
    /// Execute the migration logic. Handle your own exceptions.
    /// </summary>
    protected abstract Task ExecuteAsync(DataContext context, ILogger<Program> logger);


    public async Task RunAsync(DataContext context, ILogger<Program> logger)
    {
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
