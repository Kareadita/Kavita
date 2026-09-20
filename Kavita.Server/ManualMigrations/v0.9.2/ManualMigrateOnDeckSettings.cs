using System;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

public class ManualMigrateOnDeckSettings: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateOnDeckSettings);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var onDeckUpdateDays = await GetIntServerSetting(context, ServerSettingKey.OnDeckUpdateDays, 7, logger);
        var onDeckProgressDays = await GetIntServerSetting(context, ServerSettingKey.OnDeckProgressDays, 30, logger);

        await context.AppUserPreferences
            .ExecuteUpdateAsync(s
                => s.SetProperty(p => p.OnDeckUpdateDays, onDeckUpdateDays)
                    .SetProperty(p => p.OnDeckProgressDays, onDeckProgressDays));
    }

    private async Task<int> GetIntServerSetting(DataContext context, ServerSettingKey key, int fallback, ILogger<Program> logger)
    {
        var value = await context.ServerSetting.FirstOrDefaultAsync(s => s.Key == key);
        if (value is null)
        {
            logger.LogWarning("No setting found for {Key}, falling back to {Fallback}", key, fallback);
            return fallback;
        }

        try
        {
            return int.Parse(value.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse server setting {Key} - {Value}, falling back to {Fallback}", key, value.Value, fallback);
        }

        return fallback;
    }
}
