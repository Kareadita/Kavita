using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._8._9;

public class MigrateIncorrectUtcTimes: ManualMigration
{
    private const int BatchSize = 1000;
    protected override string MigrationName { get; } = nameof(MigrateIncorrectUtcTimes);

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

                var corrected = false;

                var wantedUtc = TimeZoneInfo.ConvertTimeToUtc(session.EndTime.Value);
                if (session.EndTimeUtc != wantedUtc)
                {
                    session.EndTimeUtc = wantedUtc;
                    context.Entry(session).State = EntityState.Modified;

                    corrected = true;
                }

                if (!TimeZoneInfo.Local.IsInvalidTime(session.StartTime))
                {
                    session.StartTime = session.StartTime.AddHours(-1 * session.StartTime.Hour);
                }

                if (!TimeZoneInfo.Local.IsInvalidTime(session.StartTime))
                {
                    logger.LogWarning("Session {Id} has invalid start time {Time} not fixing UTC", session.Id, session.StartTime);
                    continue;
                }

                var wantedStartUtc = TimeZoneInfo.ConvertTimeToUtc(session.StartTime);
                if (session.StartTimeUtc != wantedStartUtc)
                {
                    session.StartTimeUtc = wantedStartUtc;
                    context.Entry(session).State = EntityState.Modified;

                    corrected = true;
                }

                if (corrected)
                    correctedEntries++;
            }

            await context.SaveChangesAsync();

            skip += BatchSize;
        }

        logger.LogInformation("Corrected {Count} session records", correctedEntries);
    }
}
