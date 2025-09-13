using System;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// For the v0.7.14 release, one of the nightlies had bad data that would cause issues. This drops those records
/// </summary>
public static class MigrateClearNightlyExternalSeriesRecords
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateClearNightlyExternalSeriesRecords"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateClearNightlyExternalSeriesRecords migration - Please be patient, this may take some time. This is not an error");

        dataContext.ExternalSeriesMetadata.RemoveRange(dataContext.ExternalSeriesMetadata);
        dataContext.ExternalRating.RemoveRange(dataContext.ExternalRating);
        dataContext.ExternalRecommendation.RemoveRange(dataContext.ExternalRecommendation);
        dataContext.ExternalReview.RemoveRange(dataContext.ExternalReview);

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateClearNightlyExternalSeriesRecords",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateClearNightlyExternalSeriesRecords migration - Completed. This is not an error");
    }
}
