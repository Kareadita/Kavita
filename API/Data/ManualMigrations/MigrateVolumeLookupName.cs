using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public static class MigrateVolumeLookupName
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateVolumeLookupName"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateVolumeLookupName migration - Please be patient, this may take some time. This is not an error");

        // Update all volumes to have LookupName as after this migration, name isn't used for lookup
        var volumes = dataContext.Volume.ToList();
        foreach (var volume in volumes)
        {
            volume.LookupName = volume.Name;
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateVolumeLookupName",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running MigrateVolumeLookupName migration - Completed. This is not an error");
    }
}
