﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
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
    public int ProgressId { get; set; }
}

/// <summary>
/// v0.8.0 migration to move Specials into their own volume and retain user progress.
/// </summary>
public static class MigrateMixedSpecials
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, IDirectoryService directoryService, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateMixedSpecials"))
        {
            return;
        }

        logger.LogCritical(
            "Running ManualMigrateMixedSpecials migration - Please be patient, this may take some time. This is not an error");

        // First, group all the progresses into different series
        // Get each series and move the specials from old volume to the new Volume()
        // Create a new progress event from existing and store the Id of existing progress event to delete it
        // Save per series

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var extension = settings.EncodeMediaAs.GetExtension();

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
                VolumeId = p.VolumeId,
                ProgressId = p.Id
            })
            .Where(d => d.IsSpecial || d.Number == "0")
            .Join(dataContext.Volume, d => d.VolumeId, v => v.Id,
                (d, v) => new
            {
                ProgressRecord = d,
                Volume = v
            })
            .Where(d => d.Volume.Name == "0")
            .ToListAsync();

        // First, group all the progresses into different series
        logger.LogCritical("Migrating {Count} progress events to new Volume structure for Specials - This may take over 10 minutes depending on size of DB. Please wait", progress.Count);
        var progressesGroupedBySeries = progress.GroupBy(p => p.ProgressRecord.SeriesId);

        foreach (var seriesGroup in progressesGroupedBySeries)
        {
            // Get each series and move the specials from the old volume to the new Volume
            var seriesId = seriesGroup.Key;

            // Handle All Specials
            var specialsInSeries = seriesGroup
                .Where(p => p.ProgressRecord.IsSpecial)
                .ToList();

            // Get distinct Volumes by Id. For each one, create it then create the progress events
            var distinctVolumes = specialsInSeries.DistinctBy(d => d.Volume.Id);
            foreach (var distinctVolume in distinctVolumes)
            {
                // Create a new volume for each series with the appropriate number (-100000)
                var chapters = await dataContext.Chapter
                    .Where(c => c.VolumeId == distinctVolume.Volume.Id && c.IsSpecial).ToListAsync();

                var newVolume = new VolumeBuilder(Parser.SpecialVolume)
                    .WithSeriesId(seriesId)
                    .WithCreated(distinctVolume.Volume.Created)
                    .WithLastModified(distinctVolume.Volume.LastModified)
                    .Build();

                newVolume.Pages = chapters.Sum(c => c.Pages);
                newVolume.WordCount = chapters.Sum(c => c.WordCount);
                newVolume.MinHoursToRead = chapters.Sum(c => c.MinHoursToRead);
                newVolume.MaxHoursToRead = chapters.Sum(c => c.MaxHoursToRead);
                newVolume.AvgHoursToRead = chapters.Sum(c => c.AvgHoursToRead);

                dataContext.Volume.Add(newVolume);
                await dataContext.SaveChangesAsync(); // Save changes to generate the newVolumeId

                // Migrate the progress event to the new volume
                var oldVolumeProgresses = await dataContext.AppUserProgresses
                    .Where(p => p.VolumeId == distinctVolume.Volume.Id).ToListAsync();
                foreach (var oldProgress in oldVolumeProgresses)
                {
                    oldProgress.VolumeId = newVolume.Id;
                }


                logger.LogInformation("Moving {Count} chapters from Volume Id {OldVolumeId} to New Volume {NewVolumeId}",
                    chapters.Count, distinctVolume.Volume.Id, newVolume.Id);

                // Move the special chapters from the old volume to the new Volume
                foreach (var specialChapter in chapters)
                {
                    // Update the VolumeId on the existing progress event
                    specialChapter.VolumeId = newVolume.Id;

                    //UpdateCoverImage(directoryService, logger, specialChapter, extension, newVolume);
                }

                var oldVolumeBookmarks = await dataContext.AppUserBookmark
                    .Where(p => p.VolumeId == distinctVolume.Volume.Id).ToListAsync();
                logger.LogInformation("Moving {Count} existing Bookmarks from Volume Id {OldVolumeId} to New Volume {NewVolumeId}",
                    oldVolumeBookmarks.Count, distinctVolume.Volume.Id, newVolume.Id);
                foreach (var bookmark in oldVolumeBookmarks)
                {
                    bookmark.VolumeId = newVolume.Id;
                }


                var oldVolumePersonalToC = await dataContext.AppUserTableOfContent
                    .Where(p => p.VolumeId == distinctVolume.Volume.Id).ToListAsync();
                logger.LogInformation("Moving {Count} existing Personal ToC from Volume Id {OldVolumeId} to New Volume {NewVolumeId}",
                    oldVolumePersonalToC.Count, distinctVolume.Volume.Id, newVolume.Id);
                foreach (var pToc in oldVolumePersonalToC)
                {
                    pToc.VolumeId = newVolume.Id;
                }

                var oldVolumeReadingListItems = await dataContext.ReadingListItem
                    .Where(p => p.VolumeId == distinctVolume.Volume.Id).ToListAsync();
                logger.LogInformation("Moving {Count} existing Personal ToC from Volume Id {OldVolumeId} to New Volume {NewVolumeId}",
                    oldVolumeReadingListItems.Count, distinctVolume.Volume.Id, newVolume.Id);
                foreach (var readingListItem in oldVolumeReadingListItems)
                {
                    readingListItem.VolumeId = newVolume.Id;
                }

                await dataContext.SaveChangesAsync();
            }


        }

        // Save changes after processing all series
        if (dataContext.ChangeTracker.HasChanges())
        {
            await dataContext.SaveChangesAsync();
        }


        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "ManualMigrateMixedSpecials",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running ManualMigrateMixedSpecials migration - Completed. This is not an error");
    }

    private static void UpdateCoverImage(IDirectoryService directoryService, ILogger<Program> logger, Chapter specialChapter,
        string extension, Volume newVolume)
    {
        // We need to migrate cover images as well
        var existingCover = ImageService.GetChapterFormat(specialChapter.Id, specialChapter.VolumeId) + extension;
        var newCover = ImageService.GetChapterFormat(specialChapter.Id, newVolume.Id) + extension;
        try
        {

            if (!specialChapter.CoverImageLocked)
            {
                // First rename existing cover
                File.Copy(Path.Join(directoryService.CoverImageDirectory, existingCover), Path.Join(directoryService.CoverImageDirectory, newCover));
                specialChapter.CoverImage = newCover;
            }
        } catch (Exception ex)
        {
            logger.LogError(ex, "Unable to rename {OldCover} to {NewCover}, this cover will need manual refresh", existingCover, newCover);
        }
    }
}
