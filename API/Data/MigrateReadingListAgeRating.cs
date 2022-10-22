using System;
using System.Threading.Tasks;
using API.Constants;
using API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SQLitePCL;

namespace API.Data;

/// <summary>
/// New role introduced in v0.6. Calculates the Age Rating on all Reading Lists
/// </summary>
public static class MigrateReadingListAgeRating
{
    /// <summary>
    /// Will not run if any above v0.5.6.24 or v0.6.0
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="context"></param>
    /// <param name="readingListService"></param>
    /// <param name="logger"></param>
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext context, IReadingListService readingListService, ILogger<Program> logger)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (Version.Parse(settings.InstallVersion) > new Version(0, 5, 6, 26))
        {
            return;
        }

        logger.LogInformation("MigrateReadingListAgeRating migration starting");
        var readingLists = await context.ReadingList.Include(r => r.Items).ToListAsync();
        foreach (var readingList in readingLists)
        {
            await readingListService.CalculateReadingListAgeRating(readingList);
            context.ReadingList.Update(readingList);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("MigrateReadingListAgeRating migration complete");
    }
}
