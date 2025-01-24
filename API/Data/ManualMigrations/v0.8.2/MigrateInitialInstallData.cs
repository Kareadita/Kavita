using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using API.Services;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.2 I started collecting information on when the user first installed Kavita as a nice to have info for the user
/// </summary>
public static class MigrateInitialInstallData
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger, IDirectoryService directoryService)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateInitialInstallData"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateInitialInstallData migration - Please be patient, this may take some time. This is not an error");

        var settings = await dataContext.ServerSetting.ToListAsync();

        // Get the Install Date as Date the DB was written
        var dbFile = Path.Join(directoryService.ConfigDirectory, "kavita.db");
        if (!string.IsNullOrEmpty(dbFile) && directoryService.FileSystem.File.Exists(dbFile))
        {
            var fi = directoryService.FileSystem.FileInfo.New(dbFile);
            var setting = settings.First(s => s.Key == ServerSettingKey.FirstInstallDate);
            setting.Value = fi.CreationTimeUtc.ToString();
            await dataContext.SaveChangesAsync();
        }


        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateInitialInstallData",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateInitialInstallData migration - Completed. This is not an error");
    }
}
