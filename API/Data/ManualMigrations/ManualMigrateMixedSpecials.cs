using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public class UserProgressCsvRecord
{
    public bool IsSpecial { get; set; }
    public int AppUserId { get; set; }
    public int PagesRead { get; set; }
    public string Range { get; set; }
    public string Number { get; set; }
    public float MinNumber { get; set; }
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
}

/// <summary>
/// v0.8.0 migration to move Specials into their own volume and retain user progress.
/// </summary>
public class ManualMigrateMixedSpecials
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateMixedSpecials"))
        {
            return;
        }

        logger.LogCritical(
            "Running ManualMigrateMixedSpecials migration - Please be patient, this may take some time. This is not an error");

        // Get all chapters that are specials with progress
        // Store them in an csv
        // Recreate the special volumes
        var progress = await dataContext.AppUserProgresses
            .Join(dataContext.Chapter, p => p.ChapterId, c => c.Id, (p, c) => new UserProgressCsvRecord
            {
                IsSpecial = c.IsSpecial,
                AppUserId = p.AppUserId,
                PagesRead = p.PagesRead,
                Range = c.Range,
                Number = c.Number,
                MinNumber = c.MinNumber,
                SeriesId = p.SeriesId,
                VolumeId = p.VolumeId
            })
            .Where(d => d.IsSpecial || d.Number == "0")
            .ToListAsync();


        // dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        // {
        //     Name = "ManualMigrateMixedSpecials",
        //     ProductVersion = BuildInfo.Version.ToString(),
        //     RanAt = DateTime.UtcNow
        // });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running ManualMigrateMixedSpecials migration - Completed. This is not an error");
    }
}
