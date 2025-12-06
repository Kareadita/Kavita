using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using API.Entities.Progress;
using API.Services.Reading;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;


/// <summary>
/// v0.8.9 - Convert past progress into Reading Sessions
/// </summary>
public class MigrateProgressToReadingSessions : ManualMigration
{
    private const int BatchSize = 1000;
    public const string Name = nameof(MigrateProgressToReadingSessions);

    private static AppUserReadingSessionActivityData CreateSessionActivityDataFromProgress(AppUserProgress progress, Chapter chapter, MangaFormat format)
    {

        var sessionDate = progress.LastModified.Date;
        var sessionDateUtc = progress.LastModifiedUtc.Date;

        var totalWordsRead = 0;
        var isEpub = format == MangaFormat.Epub;

        if (isEpub && chapter.WordCount > 0 && chapter.Pages > 0)
        {
            totalWordsRead = (int)Math.Round(chapter.WordCount * (progress.PagesRead / (1.0f * chapter.Pages)));
        }

        // NOTE: I'm seeing a lot of off by 1 pages read issues here

        var estimatedTime = ReaderService.GetTimeEstimate(totalWordsRead, progress.PagesRead, isEpub);

        var endDate = new DateTime(
            Math.Min(
                sessionDate.AddHours(estimatedTime.AvgHours).Ticks,
                sessionDate.Date.AddDays(1).AddTicks(-1).Ticks
            ), DateTimeKind.Local
        );
        var endDateUtc = new DateTime(
            Math.Min(
                sessionDateUtc.AddHours(estimatedTime.AvgHours).Ticks,
                sessionDateUtc.Date.AddDays(1).AddTicks(-1).Ticks
            ), DateTimeKind.Utc
        );


        return new AppUserReadingSessionActivityData
        {
            ChapterId = progress.ChapterId,
            VolumeId = progress.VolumeId,
            SeriesId = progress.SeriesId,
            LibraryId = progress.LibraryId,
            StartPage = 0,
            EndPage = progress.PagesRead,
            StartBookScrollId = null,
            EndBookScrollId = progress.BookScrollId,
            StartTime = sessionDate,
            StartTimeUtc = sessionDateUtc,
            EndTime = endDate,
            EndTimeUtc = endDateUtc,
            PagesRead = progress.PagesRead,
            WordsRead = totalWordsRead,
            TotalPages = chapter.Pages,
            TotalWords = chapter.WordCount,
            ClientInfo = null,
            DeviceIds = []
        };
    }

    protected override string MigrationName => nameof(MigrateProgressToReadingSessions);
    protected override async Task ExecuteAsync(DataContext dataContext, ILogger<Program> logger)
    {
        try
        {
            var totalProgressRecords = await dataContext.AppUserProgresses.CountAsync();
            if (totalProgressRecords > 0)
            {
                logger.LogInformation("Found {Count} progress records to migrate", totalProgressRecords);

                var totalBatches = (int)Math.Ceiling(totalProgressRecords / (double)BatchSize);
                var migratedCount = 0;

                for (var batchNumber = 0; batchNumber < totalBatches; batchNumber++)
                {
                    // Join with Chapter to get TotalPages and WordCount
                    var progressBatch = await dataContext.AppUserProgresses
                        .AsNoTracking()
                        .Where(p => p.PagesRead > 0)
                        .OrderBy(p => p.Id)
                        .Skip(batchNumber * BatchSize)
                        .Take(BatchSize)
                        .Join(dataContext.Chapter,
                            p => p.ChapterId,
                            c => c.Id,
                            (progress, chapter) => new { Progress = progress, Chapter = chapter })
                        .Join(dataContext.Series,
                            p => p.Progress.SeriesId,
                            s => s.Id,
                            (combo, series) => new {combo.Progress, combo.Chapter, series.Format })
                        .ToListAsync();

                    var sessions = new List<AppUserReadingSession>();

                    var groupedProgress = progressBatch
                        .Where(item => item.Progress.PagesRead > 0)
                        .GroupBy(item => new
                        {
                            item.Progress.AppUserId,
                            item.Progress.LastModified.Date,
                        });

                    foreach (var group in groupedProgress)
                    {
                        var firstItem = group.FirstOrDefault();
                        if (firstItem == null) continue;

                        var sessionDate = firstItem.Progress.LastModified.Date;
                        var sessionDateUtc = firstItem.Progress.LastModifiedUtc.Date;

                        var activityData = group.Select(item =>
                            CreateSessionActivityDataFromProgress(item.Progress, item.Chapter, item.Format))
                            .ToList();

                        var session = new AppUserReadingSession
                        {
                            AppUserId = group.Key.AppUserId,
                            StartTime = sessionDate,
                            StartTimeUtc = sessionDateUtc,
                            EndTime = activityData.Max(a => a.EndTime),
                            EndTimeUtc = activityData.Max(a => a.EndTimeUtc),
                            IsActive = false,
                            ActivityData = activityData,
                            Created = sessionDate,
                            CreatedUtc = sessionDateUtc,
                            LastModified = sessionDate,
                            LastModifiedUtc = sessionDateUtc
                        };

                        sessions.Add(session);
                    }

                    if (sessions.Count > 0)
                    {
                        await dataContext.AppUserReadingSession.AddRangeAsync(sessions);
                        await dataContext.SaveChangesAsync();
                        migratedCount += sessions.Count;
                    }

                    logger.LogInformation("Migrated batch {Current}/{Total} ({Count} sessions)",
                        batchNumber + 1, totalBatches, migratedCount);
                }

                logger.LogInformation("Migration complete: {Count} sessions created from {Total} progress records",
                    migratedCount, totalProgressRecords);
            }
            else
            {
                logger.LogInformation("No progress records found to migrate");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during MigrateProgressToReadingSessions migration");
            throw;
        }
    }
}
