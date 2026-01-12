using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Progress;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Helpers.Builders;
using API.Services.Reading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ReadingHistoryServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private ReadingHistoryService Setup(DataContext context)
    {
        return new ReadingHistoryService(context, Substitute.For<ILogger<ReadingHistoryService>>());
    }

    [Fact]
    public async Task ActiveSession_DoNotCreateHistoryItems()
    {
        var (_, dataContext, _) = await CreateDatabase();
        var service = Setup(dataContext);

        // Setup data
        var lib = await dataContext.Library.FirstAsync();
        lib.Series.Add(new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithPages(2).Build()).Build())
            .Build());

        await dataContext.AppUser.AddAsync(new AppUser() { UserName = "Test" });

        await  dataContext.SaveChangesAsync();

        // Create an active session dated for yesterday
        var yesterday= DateTime.Now.Date.AddDays(-1);
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);
        await dataContext.AppUserReadingSession.AddAsync(new AppUserReadingSession()
        {
            ActivityData =
            [
                new AppUserReadingSessionActivityData(new ProgressDto()
                {
                    ChapterId = 1, VolumeId = 1, LibraryId = 1, PageNum = 1, SeriesId = 1
                }, 1, MangaFormat.Archive)
            ],
            AppUserId = 1,
            StartTime = yesterday,
            StartTimeUtc = yesterdayUtc,
            IsActive = true,
        });

        // Run the service
        await service.AggregateYesterdaysActivity();

        // Check that there are no history items
        Assert.False(await dataContext.AppUserReadingHistory.AnyAsync());
    }


    [Fact]
    public async Task CreatesForYesterdaySessions()
    {
        var (_, dataContext, _) = await CreateDatabase();
        var service = Setup(dataContext);

        // Setup data
        var lib = await dataContext.Library.FirstAsync();
        lib.Series.Add(new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithPages(2).Build()).Build())
            .Build());

        await dataContext.AppUser.AddAsync(new AppUser() { UserName = "Test" });

        await  dataContext.SaveChangesAsync();

        // Create an active session dated for yesterday
        var yesterday= DateTime.Now.Date.AddDays(-1);
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);
        var activityData = new AppUserReadingSessionActivityData(new ProgressDto()
        {
            ChapterId = 1, VolumeId = 1, LibraryId = 1, PageNum = 1, SeriesId = 1
        }, 1, MangaFormat.Archive);

        activityData.StartTime = yesterday;
        activityData.StartTimeUtc = yesterdayUtc;

        await dataContext.AppUserReadingSession.AddAsync(new AppUserReadingSession()
        {
            ActivityData =
            [
                activityData
            ],
            AppUserId = 1,
            StartTime = yesterday,
            StartTimeUtc = yesterdayUtc,
            EndTime = yesterday.AddHours(1),
            EndTimeUtc = yesterdayUtc.AddHours(1),
            IsActive = false,
        });

        await  dataContext.SaveChangesAsync();

        // Run the service
        await service.AggregateYesterdaysActivity();

        // Check that there are no history items
        var historyItems = await dataContext.AppUserReadingHistory.ToListAsync();
        Assert.Single(historyItems);
        Assert.Single(historyItems[0].Data.Activities);
    }
}

