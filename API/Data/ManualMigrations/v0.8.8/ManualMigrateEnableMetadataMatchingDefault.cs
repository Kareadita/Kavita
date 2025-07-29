using System;
using System.Threading.Tasks;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.8 - If Kavita+ users had Metadata Matching settings already, ensure the new non-Kavita+ system is enabled to match
/// existing experience
/// </summary>
public static class ManualMigrateEnableMetadataMatchingDefault
{
    public static async Task Migrate(DataContext context, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateEnableMetadataMatchingDefault"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateEnableMetadataMatchingDefault migration - Please be patient, this may take some time. This is not an error");

        var settings = await unitOfWork.SettingsRepository.GetMetadataSettingDto();


        var shouldBeEnabled = settings != null && (settings.Enabled || settings.AgeRatingMappings.Count != 0 ||
                                                   settings.Blacklist.Count != 0 || settings.Whitelist.Count != 0 ||
                                                   settings.Whitelist.Count != 0 || settings.Blacklist.Count != 0 ||
                                                   settings.FieldMappings.Count != 0);

        if (shouldBeEnabled && !settings.EnableExtendedMetadataProcessing)
        {
            var mSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
            mSettings.EnableExtendedMetadataProcessing = shouldBeEnabled;
            await unitOfWork.CommitAsync();
        }


        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "ManualMigrateEnableMetadataMatchingDefault",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogCritical("Running ManualMigrateEnableMetadataMatchingDefault migration - Completed. This is not an error");
    }
}
