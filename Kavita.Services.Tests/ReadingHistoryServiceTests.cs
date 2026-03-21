using Kavita.API.Repositories;
using Kavita.Database;
using Kavita.Database.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Reading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

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
        var lib = await dataContext.Library.Includes(LibraryIncludes.Series).FirstAsync();
        lib.Series.Add(new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithPages(2).Build()).Build())
            .Build());

        await dataContext.AppUser.AddAsync(new AppUser() { UserName = "Test" });

        await  dataContext.SaveChangesAsync();

        // Create an active session dated for yesterday
        var yesterday= DateTime.Now.Date.AddDays(-1);
        var yesterdayUtc = yesterday.ToUniversalTime();
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
        var lib = await dataContext.Library.Includes(LibraryIncludes.Series).FirstAsync();
        lib.Series.Add(new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithPages(2).Build()).Build())
            .Build());

        await dataContext.AppUser.AddAsync(new AppUser() { UserName = "Test" });

        await  dataContext.SaveChangesAsync();

        // Create an active session dated for yesterday
        var yesterday= DateTime.Now.Date.AddDays(-1);
        var yesterdayUtc = yesterday.ToUniversalTime();
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

        // Check that there is exactly one item
        var historyItems = await dataContext.AppUserReadingHistory.ToListAsync();
        Assert.Single(historyItems);
        Assert.Single(historyItems[0].Data.Activities);
    }
}

