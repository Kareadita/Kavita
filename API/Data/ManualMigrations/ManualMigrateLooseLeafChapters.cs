using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;


/// <summary>
/// v0.8.0 migration to move loose leaf chapters into their own volume and retain user progress.
/// </summary>
public static class MigrateLooseLeafChapters
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateLooseLeafChapters"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateLooseLeafChapters migration - Please be patient, this may take some time. This is not an error");

        // First, group all the progresses into different series

        // Get each series and move the specials from old volume to the new Volume()

        // Create a new progress event from existing and store the Id of existing progress event to delete it

        // Save per series

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
            .Where(d => !d.IsSpecial)
            .Join(dataContext.Volume, d => d.VolumeId, v => v.Id, (d, v) => new
            {
                ProgressRecord = d,
                Volume = v
            })
            .Where(d => d.Volume.Name == "0")
            .ToListAsync();

        // First, group all the progresses into different series
        logger.LogCritical("Migrating {Count} progress events to new Volume structure - This may take over 10 minutes depending on size of DB. Please wait", progress.Count);
        var progressesGroupedBySeries = progress
            .GroupBy(p => p.ProgressRecord.SeriesId);

        foreach (var seriesGroup in progressesGroupedBySeries)
        {
            // Get each series and move the specials from the old volume to the new Volume
            var seriesId = seriesGroup.Key;

            // Handle All Specials
            var looseLeafsInSeries = seriesGroup
                .Where(p => !p.ProgressRecord.IsSpecial)
                .ToList();

            // Get distinct Volumes by Id. For each one, create it then create the progress events
            var distinctVolumes = looseLeafsInSeries.DistinctBy(d => d.Volume.Id);
            foreach (var distinctVolume in distinctVolumes)
            {
                // Create a new volume for each series with the appropriate number (-100000)
                var chapters = await dataContext.Chapter
                    .Where(c => c.VolumeId == distinctVolume.Volume.Id && !c.IsSpecial).ToListAsync();

                var newVolume = new VolumeBuilder(Parser.LooseLeafVolume)
                    .WithSeriesId(seriesId)
                    .WithChapters(chapters)
                    .Build();
                dataContext.Volume.Add(newVolume);
                await dataContext.SaveChangesAsync(); // Save changes to generate the newVolumeId

                // Migrate the progress event to the new volume
                distinctVolume.ProgressRecord.VolumeId = newVolume.Id;


                logger.LogInformation("Moving {Count} chapters from Volume Id {OldVolumeId} to New Volume {NewVolumeId}",
                    chapters.Count, distinctVolume.Volume.Id, newVolume.Id);

                // Move the loose leaf chapters from the old volume to the new Volume
                var looseLeafChapters = await dataContext.Chapter
                    .Where(c => c.VolumeId == distinctVolume.ProgressRecord.VolumeId && !c.IsSpecial)
                    .ToListAsync();



                foreach (var chapter in looseLeafChapters)
                {
                    // Update the VolumeId on the existing progress event
                    chapter.VolumeId = newVolume.Id;
                }
                await dataContext.SaveChangesAsync();
            }
        }

        // Save changes after processing all series
        if (dataContext.ChangeTracker.HasChanges())
        {
            await dataContext.SaveChangesAsync();
        }


        // dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        // {
        //     Name = "MigrateLooseLeafChapters",
        //     ProductVersion = BuildInfo.Version.ToString(),
        //     RanAt = DateTime.UtcNow
        // });
        //
        // await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running MigrateLooseLeafChapters migration - Completed. This is not an error");
    }
}
