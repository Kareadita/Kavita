using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Services.Reading;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Reader;
using Kavita.Services.Builders;
using Kavita.Services.Reading;
using Kavita.Services.Tests.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Reading;

public class ReadingSessionServiceBackfillTests(ITestOutputHelper helper): AbstractDbTest(helper)
{

    private static HourEstimateRangeDto AvgEstimate(float hours, int pages = 0, long words = 0) => new HourEstimateRangeDto
    {
        AvgHours = hours,
        PageCount = pages,
        WordCount = words
    };

    private async Task<(IDataContext, IReadingSessionService, IReaderService)> Setup()
    {
        var (_, context, mapper) = await CreateDatabase();

        var user = new AppUserBuilder("Amelia", "amelia@localhot").Build();
        await context.AppUser.AddAsync(user);
        await context.SaveChangesAsync();

        var readerService = Substitute.For<IReaderService>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IReaderService)).Returns(readerService);
        serviceProvider.GetService(typeof(IMapper)).Returns(mapper);
        serviceProvider.GetService(typeof(IDataContext)).Returns(context);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var readingSessionService = new ReadingSessionService(
            scopeFactory,
            Substitute.For<ILogger<ReadingSessionService>>(),
            new FakeHybridCache()
        );

        return (context, readingSessionService, readerService);

    }

    [Fact]
    public void ScheduleChapters_AllFitInCurrentDay_ReturnsCorrectSchedule()
    {
        var currentEnd = new DateTime(2026, 3, 17, 14, 0, 0); // 14:00
        var chapters = new Dictionary<int, HourEstimateRangeDto>
        {
            { 1, AvgEstimate(2f) }, // 2 hours
            { 2, AvgEstimate(3f) }, // 3 hours
            { 3, AvgEstimate(1f) }, // 1 hour
        };
        // Total = 6 hours, all fit within 14:00 on the same day

        var schedule = ReadingSessionService.ScheduleChapters(chapters, currentEnd);

        Assert.Equal(new DateTime(2026, 3, 17, 12, 0, 0), schedule[1].Start);
        Assert.Equal(new DateTime(2026, 3, 17, 14, 0, 0), schedule[1].End);

        Assert.Equal(new DateTime(2026, 3, 17,  9, 0, 0), schedule[2].Start);
        Assert.Equal(new DateTime(2026, 3, 17, 12, 0, 0), schedule[2].End);

        Assert.Equal(new DateTime(2026, 3, 17,  8, 0, 0), schedule[3].Start);
        Assert.Equal(new DateTime(2026, 3, 17,  9, 0, 0), schedule[3].End);
    }

    [Fact]
    public void ScheduleChapters_SingleChapter_FitsExactlyFromMidnight()
    {
        // Arrange
        var currentEnd = new DateTime(2026, 3, 17, 14, 0, 0); // 14:00
        var chapters = new Dictionary<int, HourEstimateRangeDto>
        {
            { 1, AvgEstimate(14f) }, // exactly 14 hours — starts precisely at midnight
        };

        var schedule = ReadingSessionService.ScheduleChapters(chapters, currentEnd);

        Assert.Equal(new DateTime(2026, 3, 17,  0, 0, 0), schedule[1].Start);
        Assert.Equal(new DateTime(2026, 3, 17, 14, 0, 0), schedule[1].End);
    }

    [Fact]
    public void ScheduleChapters_ChapterDoesNotFitInCurrentDay_SnapsBackToPreviousDay()
    {
        // Arrange
        var currentEnd = new DateTime(2026, 3, 17, 6, 0, 0); // 06:00
        var chapters = new Dictionary<int, HourEstimateRangeDto>
        {
            { 1, AvgEstimate(4f)  }, // 4 hours — fits: 02:00–06:00 Mar 17
            { 2, AvgEstimate(10f) }, // 10 hours — does not fit: would start Mar 16, snaps back to end at midnight Mar 17
            { 3, AvgEstimate(3f)  }, // 3 hours — behind prev
        };

        var schedule = ReadingSessionService.ScheduleChapters(chapters, currentEnd);

        // Chapter 1: fits normally
        Assert.Equal(new DateTime(2026, 3, 17,  2, 0, 0), schedule[1].Start);
        Assert.Equal(new DateTime(2026, 3, 17,  6, 0, 0), schedule[1].End);

        // Chapter 2: would start at 16:00 Mar 16, but spans midnight — snaps to end at 00:00 Mar 17
        Assert.Equal(new DateTime(2026, 3, 16, 14, 0, 0), schedule[2].Start);
        Assert.Equal(new DateTime(2026, 3, 17,  0, 0, 0), schedule[2].End);

        // Chapter 3: continues back from 14:00 Mar 16
        Assert.Equal(new DateTime(2026, 3, 16, 11, 0, 0), schedule[3].Start);
        Assert.Equal(new DateTime(2026, 3, 16, 14, 0, 0), schedule[3].End);
    }

    [Fact]
    public async Task GenerateReadingSessionForChapters_NoReadingToDo_NoSessions()
    {
        var (context, readingSessionService, readerService) = await Setup();

        var lib = await context.Library.FirstAsync();

        var series = new SeriesBuilder("Long-Awaited Feelings")
            .WithLibraryId(lib.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithPages(100)
                    .Build())
                .Build())
            .Build();

        context.Series.Add(series);
        await context.SaveChangesAsync();

        // Return no estimate for chapter 1
        readerService.GetEstimateFromPageForChapter(1, 1, 1, 0)
            .Returns(AvgEstimate(0f));

        await readingSessionService.GenerateReadingSessionForChapters(1, 1, new Dictionary<int, int> { { 1, 0 }});

        Assert.Empty(await context.AppUserReadingSession.ToListAsync());
    }

    [Fact]
    public async Task GenerateReadingSessionForChapters_ChaptersAreSortedBySortOrder_SessionsScheduledHighestSortOrderFirst()
    {
        // Chapter 2 has a higher SortOrder and should be scheduled "earlier" in time
        // (i.e. it ends later, closer to "now"), with chapter 1 behind it.
        var (context, readingSessionService, readerService) = await Setup();
        var lib = await context.Library.FirstAsync();

        var series = new SeriesBuilder("Sort Order Test")
            .WithLibraryId(lib.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithSortOrder(1)
                    .WithPages(100)
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithSortOrder(2)
                    .WithPages(100)
                    .Build())
                .Build())
            .Build();

        context.Series.Add(series);
        await context.SaveChangesAsync();

        var ch1 = series.Volumes[0].Chapters.First(c => c.SortOrder == 1);
        var ch2 = series.Volumes[0].Chapters.First(c => c.SortOrder == 2);

        readerService.GetEstimateFromPageForChapter(1, series.Id, ch1.Id, 0)
            .Returns(AvgEstimate(1f, 10)); // 1 hour
        readerService.GetEstimateFromPageForChapter(1, series.Id, ch2.Id, 0)
            .Returns(AvgEstimate(1f, 10)); // 1 hour

        var user = await context.AppUser.FirstAsync();
        await readingSessionService.GenerateReadingSessionForChapters(user.Id, series.Id, new Dictionary<int, int>
        {
            { ch1.Id, 0 },
            { ch2.Id, 0 },
        });

        var session = await context.AppUserReadingSession.SingleAsync();

        var ch1SessionData = session.ActivityData.FirstOrDefault(d => d.ChapterId == ch1.Id);
        var ch2SessionData = session.ActivityData.FirstOrDefault(d => d.ChapterId == ch2.Id);

        Assert.NotNull(ch1SessionData);
        Assert.NotNull(ch2SessionData);

        Assert.True(ch1SessionData.EndTime <= ch2SessionData.EndTime, "Chapter 1 must finish before chapter 2 starts");
    }

    [Fact]
    public async Task GenerateReadingSessionForChapters_EstimatesAreUsed_ActivityDataDurationMatchesEstimate()
    {
        var (context, readingSessionService, readerService) = await Setup();
        var lib = await context.Library.FirstAsync();

        var series = new SeriesBuilder("Estimate Test")
            .WithLibraryId(lib.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithSortOrder(1)
                    .WithPages(100)
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithSortOrder(2)
                    .WithPages(100)
                    .Build())
                .Build())
            .Build();

        context.Series.Add(series);
        await context.SaveChangesAsync();

        var ch1 = series.Volumes[0].Chapters.First(c => c.SortOrder == 1);
        var ch2 = series.Volumes[0].Chapters.First(c => c.SortOrder == 2);

        const float ch1Hours = 2f;
        const float ch2Hours = 3f;

        readerService.GetEstimateFromPageForChapter(1, series.Id, ch1.Id, 0)
            .Returns(AvgEstimate(ch1Hours, 100));
        readerService.GetEstimateFromPageForChapter(1, series.Id, ch2.Id, 0)
            .Returns(AvgEstimate(ch2Hours, 100));

        var user = await context.AppUser.FirstAsync();
        await readingSessionService.GenerateReadingSessionForChapters(user.Id, series.Id, new Dictionary<int, int>
        {
            { ch1.Id, 0 },
            { ch2.Id, 0 },
        });

        var session = await context.AppUserReadingSession.SingleAsync();

        var ch1Data = session.ActivityData.FirstOrDefault(d => d.ChapterId == ch1.Id);
        var ch2Data = session.ActivityData.FirstOrDefault(d => d.ChapterId == ch2.Id);

        Assert.NotNull(ch1Data);
        Assert.NotNull(ch2Data);

        Assert.Equal(TimeSpan.FromHours(ch1Hours), ch1Data.EndTime - ch1Data.StartTime);
        Assert.Equal(TimeSpan.FromHours(ch2Hours), ch2Data.EndTime - ch2Data.StartTime);
    }

    [Fact]
    public async Task GenerateReadingSessionForChapters_TotalTimeExceeds24Hours_TwoSessionsCreated()
    {
        // 13h + 13h = 26h, guarantees a split regardless of time-of-day
        var (context, readingSessionService, readerService) = await Setup();
        var lib = await context.Library.FirstAsync();

        var series = new SeriesBuilder("Multi-Day Test")
            .WithLibraryId(lib.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithSortOrder(1)
                    .WithPages(100)
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithSortOrder(2)
                    .WithPages(100)
                    .Build())
                .Build())
            .Build();

        context.Series.Add(series);
        await context.SaveChangesAsync();

        var ch1 = series.Volumes[0].Chapters.First(c => c.SortOrder == 1);
        var ch2 = series.Volumes[0].Chapters.First(c => c.SortOrder == 2);

        readerService.GetEstimateFromPageForChapter(1, series.Id, ch1.Id, 0)
            .Returns(AvgEstimate(13f, 100));
        readerService.GetEstimateFromPageForChapter(1, series.Id, ch2.Id, 0)
            .Returns(AvgEstimate(13f, 100));

        var user = await context.AppUser.FirstAsync();
        await readingSessionService.GenerateReadingSessionForChapters(user.Id, series.Id, new Dictionary<int, int>
        {
            { ch1.Id, 0 },
            { ch2.Id, 0 },
        });

        var sessions = await context.AppUserReadingSession.ToListAsync();
        Assert.Equal(2, sessions.Count);

        // Each session must contain at least one chapter's activity
        Assert.All(sessions, s => Assert.NotEmpty(s.ActivityData));

        // The two sessions must cover different calendar days
        var allDays = sessions
            .SelectMany(s => s.ActivityData)
            .SelectMany(d => new[] { d.StartTime.Date, d.EndTime.Value.Date })
            .Distinct()
            .ToList();

        Assert.True(allDays.Count >= 2,
            "Sessions totalling more than 24h must be scheduled across at least two calendar days");
    }

}
