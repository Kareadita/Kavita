using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using Flurl.Util;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// At some point, encoding settings wrote bad data to the backend, maybe in v0.8.0. This just fixes any bad data.
/// </summary>
public static class ManualMigrateEncodeSettings
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateEncodeSettings"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateEncodeSettings migration - Please be patient, this may take some time. This is not an error");


        var encodeAs = await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.EncodeMediaAs);
        var coverSize = await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.CoverImageSize);

        var encodeMap = new Dictionary<string, string>
        {
            { EncodeFormat.WEBP.ToString(), ((int)EncodeFormat.WEBP).ToString() },
            { EncodeFormat.PNG.ToString(), ((int)EncodeFormat.PNG).ToString() },
            { EncodeFormat.AVIF.ToString(), ((int)EncodeFormat.AVIF).ToString() }
        };

        if (encodeMap.TryGetValue(encodeAs.Value, out var encodedValue))
        {
            encodeAs.Value = encodedValue;
            context.ServerSetting.Update(encodeAs);
        }

        if (coverSize.Value == "0")
        {
            coverSize.Value = ((int)CoverImageSize.Default).ToString();
            context.ServerSetting.Update(coverSize);
        }


        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateEncodeSettings",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateEncodeSettings migration - Completed. This is not an error");
    }
}
