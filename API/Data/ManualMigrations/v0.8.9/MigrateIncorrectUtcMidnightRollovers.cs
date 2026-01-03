using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public class MigrateIncorrectUtcMidnightRollovers: ManualMigration
{
    private const int BatchSize = 1000;
    protected override string MigrationName { get; } = nameof(MigrateIncorrectUtcMidnightRollovers);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var skip = 0;
        var correctedEntries = 0;

        while (true)
        {
            var batch = await context.AppUserReadingSession
                .Where(s => s.EndTime != null && s.EndTimeUtc != null)
                .OrderBy(s => s.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var session in batch)
            {
                if (session.EndTimeUtc == null || session.EndTime == null)
                {
                    continue;
                }

                var wantedUtc = TimeZoneInfo.ConvertTimeToUtc(session.EndTime.Value);
                if (session.EndTimeUtc != wantedUtc)
                {
                    session.EndTimeUtc = wantedUtc;
                    context.Entry(session).State = EntityState.Modified;

                    correctedEntries++;
                }
            }

            await context.SaveChangesAsync();

            skip += BatchSize;
        }

        logger.LogInformation("Corrected {Count} session records", correctedEntries);
    }
}
