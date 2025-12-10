using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.9 - Some AppUser rows are missing CreatedUtc date
/// </summary>
public class MigrateMissingCreatedUtcDate : ManualMigration
{
    protected override string MigrationName => nameof(MigrateMissingCreatedUtcDate);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        try
        {
            //0001-01-01 00:00:00
            var usersWithoutCorrectCreatedUtc = await context.AppUser
                .Where(u => u.CreatedUtc == DateTime.MinValue)
                .ToListAsync();

            foreach (var user in usersWithoutCorrectCreatedUtc)
            {
                user.CreatedUtc = user.Created.ToUniversalTime();
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during {Name} migration", MigrationName);
            throw;
        }
    }
}
