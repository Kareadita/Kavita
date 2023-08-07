using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Introduced in v0.7.5.6 and v0.7.6, Ratings > 0 need to have "HasRatingSet"
/// </summary>
/// <remarks>Added in v0.7.5.6</remarks>
// ReSharper disable once InconsistentNaming
public static class MigrateExistingRatings
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        logger.LogCritical("Running MigrateExistingRatings migration - Please be patient, this may take some time. This is not an error");

        foreach (var r in context.AppUserRating.Where(r => r.Rating > 0f))
        {
            r.HasBeenRated = true;
            context.Entry(r).State = EntityState.Modified;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        logger.LogCritical("Running MigrateExistingRatings migration - Completed. This is not an error");
    }
}
