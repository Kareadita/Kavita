using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Reading;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Progress;
using Kavita.Services.Builders;
using Kavita.Services.Reading;
using Kavita.Services.Tests.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Reading;

public class ReadingSessionServiceBackfillReportTests(ITestOutputHelper helper) : AbstractDbTest(helper)
{
    private const int Pages = 100;

    private sealed record Fixture(
        IDataContext Context,
        IReadingSessionService Service,
        IReaderService Reader,
        IUserRepository Users,
        IAppUserProgressRepository Progress,
        int UserId,
        int ChapterId,
        int SecondChapterId);

    private async Task<Fixture> Setup()
    {
        var (_, context, mapper) = await CreateDatabase();

        var user = new AppUserBuilder("Amelia", "amelia@localhot").Build();
        await context.AppUser.AddAsync(user);
        await context.SaveChangesAsync();

        var lib = await context.Library.FirstAsync();
        var series = new SeriesBuilder("Slow Boat")
            .WithLibraryId(lib.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(Pages).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(Pages).Build())
                .Build())
            .Build();
        context.Series.Add(series);
        await context.SaveChangesAsync();
        var chapters = series.Volumes[0].Chapters.OrderBy(c => c.Id).ToList();

        var reader = Substitute.For<IReaderService>();
        var users = Substitute.For<IUserRepository>();
        users.HasAccessToChapter(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        var progress = Substitute.For<IAppUserProgressRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserRepository.Returns(users);
        unitOfWork.AppUserProgressRepository.Returns(progress);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IReaderService)).Returns(reader);
        serviceProvider.GetService(typeof(IMapper)).Returns(mapper);
        serviceProvider.GetService(typeof(IDataContext)).Returns(context);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var service = new ReadingSessionService(scopeFactory, Substitute.For<ILogger<ReadingSessionService>>(), new FakeHybridCache());
        return new Fixture(context, service, reader, users, progress, user.Id, chapters[0].Id, chapters[1].Id);
    }

    /// <summary>Noon of a recent day in the server's time zone, so readings around it never cross midnight.</summary>
    private static DateTime DayAtNoon(int daysAgo) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.Today.AddDays(-daysAgo).AddHours(12), TimeZoneInfo.Local);

    private static BackfillReadingEntryDto Entry(int chapterId, DateTime start, int minutes, int startPage = 1, int endPage = 10) => new()
    {
        ChapterId = chapterId,
        StartPage = startPage,
        EndPage = endPage,
        StartTimeUtc = start,
        EndTimeUtc = start.AddMinutes(minutes),
    };

    [Fact]
    public async Task BackfillReadingSessions_RecordsAFinishedSessionWithTheClientsTimes()
    {
        var f = await Setup();
        var start = DayAtNoon(2);

        var results = await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, start, 20, startPage: 5, endPage: 15)]);

        Assert.Equal([BackfillReadingStatus.Applied], results);
        var session = await f.Context.AppUserReadingSession.Include(s => s.ActivityData).SingleAsync();
        Assert.True(session.IsBackfilled);
        Assert.False(session.IsActive);
        Assert.False(session.IsGenerated);
        Assert.Equal(start, session.StartTimeUtc);
        Assert.Equal(start.AddMinutes(20), session.EndTimeUtc);
        var activity = Assert.Single(session.ActivityData);
        Assert.Equal(f.ChapterId, activity.ChapterId);
        Assert.Equal(Pages, activity.TotalPages);
        Assert.Equal(4, activity.StartPage); // like a live session, whose first update is already one page in
        Assert.Equal(15, activity.EndPage);
        Assert.Equal(11, activity.PagesRead);
    }

    [Fact]
    public async Task BackfillReadingSessions_SendingTheSameEntryAgainAddsNothing()
    {
        var f = await Setup();
        var entry = Entry(f.ChapterId, DayAtNoon(1), 10);

        await f.Service.BackfillReadingSessions(f.UserId, [entry]);
        var again = await f.Service.BackfillReadingSessions(f.UserId, [entry]);

        Assert.Equal([BackfillReadingStatus.AlreadyRecorded], again);
        Assert.Single(await f.Context.AppUserReadingSession.ToListAsync());
    }

    [Fact]
    public async Task BackfillReadingSessions_TheSameEntryTwiceInOneRequestCountsOnce()
    {
        var f = await Setup();
        var entry = Entry(f.ChapterId, DayAtNoon(1), 10);

        var results = await f.Service.BackfillReadingSessions(f.UserId, [entry, entry]);

        Assert.Equal([BackfillReadingStatus.Applied, BackfillReadingStatus.AlreadyRecorded], results);
        Assert.Single(await f.Context.AppUserReadingSessionActivityData.ToListAsync());
    }

    [Fact]
    public async Task BackfillReadingSessions_RefusesWhatCannotBeTrueAndKeepsTheRest()
    {
        var f = await Setup();
        var good = Entry(f.ChapterId, DayAtNoon(1), 10);
        var inFuture = Entry(f.ChapterId, DateTime.UtcNow.AddHours(1), 10);
        var tooOld = Entry(f.ChapterId, DateTime.UtcNow.AddDays(-(BackfillReadingResultDto.MaxAgeDays + 1)), 10);
        var backwards = good with { StartTimeUtc = good.EndTimeUtc, EndTimeUtc = good.StartTimeUtc };
        var negative = good with { StartPage = -1 };

        var results = await f.Service.BackfillReadingSessions(f.UserId, [inFuture, tooOld, backwards, negative, good]);

        Assert.Equal(
            [
                BackfillReadingStatus.InFuture,
                BackfillReadingStatus.TooOld,
                BackfillReadingStatus.Invalid,
                BackfillReadingStatus.Invalid,
                BackfillReadingStatus.Applied,
            ], results);
        Assert.Single(await f.Context.AppUserReadingSession.ToListAsync());
    }

    [Fact]
    public async Task BackfillReadingSessions_ChaptersTheUserCannotReadAreRefused()
    {
        var f = await Setup();
        f.Users.HasAccessToChapter(f.UserId, f.SecondChapterId, Arg.Any<CancellationToken>()).Returns(false);

        var results = await f.Service.BackfillReadingSessions(f.UserId,
            [Entry(f.SecondChapterId, DayAtNoon(1), 10), Entry(f.ChapterId, DayAtNoon(1), 10)]);

        Assert.Equal([BackfillReadingStatus.NoAccess, BackfillReadingStatus.Applied], results);
        Assert.Equal(f.ChapterId, (await f.Context.AppUserReadingSessionActivityData.SingleAsync()).ChapterId);
    }

    [Fact]
    public async Task BackfillReadingSessions_ReadingWithinTheSessionTimeoutIsOneSession()
    {
        var f = await Setup();
        var start = DayAtNoon(3);

        await f.Service.BackfillReadingSessions(f.UserId,
        [
            Entry(f.ChapterId, start, 20),
            Entry(f.SecondChapterId, start.AddMinutes(25), 20), // 5 minutes after the first ended
        ]);

        var session = await f.Context.AppUserReadingSession.Include(s => s.ActivityData).SingleAsync();
        Assert.Equal(2, session.ActivityData.Count);
        Assert.Equal(start, session.StartTimeUtc);
        Assert.Equal(start.AddMinutes(45), session.EndTimeUtc);
    }

    [Fact]
    public async Task BackfillReadingSessions_AFarLongerPauseStartsANewSession()
    {
        var f = await Setup();
        var start = DayAtNoon(3);

        await f.Service.BackfillReadingSessions(f.UserId,
        [
            Entry(f.SecondChapterId, start.AddHours(2), 20),
            Entry(f.ChapterId, start, 20),
        ]);

        var sessions = await f.Context.AppUserReadingSession.OrderBy(s => s.StartTimeUtc).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(start, sessions[0].StartTimeUtc);
        Assert.Equal(start.AddHours(2), sessions[1].StartTimeUtc);
    }

    [Fact]
    public async Task BackfillReadingSessions_ReadingOnDifferentDaysNeverSharesASession()
    {
        var f = await Setup();
        var late = TimeZoneInfo.ConvertTimeToUtc(DateTime.Today.AddDays(-3).AddHours(23).AddMinutes(50), TimeZoneInfo.Local);

        await f.Service.BackfillReadingSessions(f.UserId,
        [
            Entry(f.ChapterId, late.AddMinutes(-5), 4),
            Entry(f.SecondChapterId, late.AddMinutes(15), 10), // after midnight, 11 minutes after the first ended
        ]);

        Assert.Equal(2, await f.Context.AppUserReadingSession.CountAsync());
    }

    [Fact]
    public async Task BackfillReadingSessions_ProgressMovesToWhereTheLatestReadingEnded()
    {
        var f = await Setup();
        var start = DayAtNoon(1);
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>()).Returns((AppUserProgress?) null);

        await f.Service.BackfillReadingSessions(f.UserId,
        [
            Entry(f.ChapterId, start, 10, startPage: 1, endPage: 12),
            Entry(f.ChapterId, start.AddMinutes(15), 10, startPage: 12, endPage: 30),
        ]);

        await f.Reader.Received(1).SaveReadingProgress(
            Arg.Is<ProgressDto>(p => p.ChapterId == f.ChapterId && p.PageNum == 30),
            f.UserId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillReadingSessions_ProgressSavedMoreRecentlyIsLeftAlone()
    {
        var f = await Setup();
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>())
            .Returns(new AppUserProgress { ChapterId = f.ChapterId, AppUserId = f.UserId, PagesRead = 80, LastModifiedUtc = DateTime.UtcNow });

        var results = await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, DayAtNoon(1), 10, endPage: 12)]);

        // The reading still counts for History; it just does not take the reader back to page 12
        Assert.Equal([BackfillReadingStatus.Applied], results);
        await f.Reader.DidNotReceiveWithAnyArgs().SaveReadingProgress(default!, default, default, default);
    }

    [Fact]
    public async Task BackfillReadingSessions_FinishingAChapterCountsARead()
    {
        var f = await Setup();
        var chapter = await f.Context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series).SingleAsync(c => c.Id == f.ChapterId);
        var progress = new AppUserProgress
        {
            ChapterId = f.ChapterId,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            AppUserId = f.UserId,
            PagesRead = Pages,
            LastModifiedUtc = DateTime.UtcNow.AddDays(-5),
        };
        f.Context.AppUserProgresses.Add(progress);
        await f.Context.SaveChangesAsync();
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>()).Returns(progress);

        await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, DayAtNoon(1), 10, startPage: 90, endPage: Pages)]);

        Assert.Equal(1, (await f.Context.AppUserProgresses.AsNoTracking().SingleAsync()).TotalReads);
    }

    [Fact]
    public async Task BackfillReadingSessions_AClockAFewMinutesAheadStillMatchesWhenTheEntryIsSentAgain()
    {
        var f = await Setup();
        var entry = Entry(f.ChapterId, DateTime.UtcNow.AddMinutes(-8), 10); // ends 2 minutes from now

        var first = await f.Service.BackfillReadingSessions(f.UserId, [entry]);
        var again = await f.Service.BackfillReadingSessions(f.UserId, [entry with { EndTimeUtc = entry.EndTimeUtc }]);

        Assert.Equal([BackfillReadingStatus.Applied], first);
        Assert.Equal([BackfillReadingStatus.AlreadyRecorded], again);
        Assert.Single(await f.Context.AppUserReadingSessionActivityData.ToListAsync());
    }

    [Fact]
    public async Task BackfillReadingSessions_PagesPastTheEndOfTheChapterAreCapped()
    {
        var f = await Setup();

        await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, DayAtNoon(1), 10, startPage: 90, endPage: 1000)]);

        var activity = await f.Context.AppUserReadingSessionActivityData.SingleAsync();
        Assert.Equal(Pages, activity.EndPage);
        Assert.Equal(Pages - 89, activity.PagesRead);
    }

    [Fact]
    public async Task BackfillReadingSessions_ASessionDoesNotCarryOverMidnight()
    {
        var f = await Setup();
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.Today.AddDays(-2).AddHours(23).AddMinutes(50), TimeZoneInfo.Local);

        await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, start, 30)]);

        var session = await f.Context.AppUserReadingSession.Include(s => s.ActivityData).SingleAsync();
        Assert.Equal(session.StartTime.Date, session.EndTime!.Value.Date);
        Assert.Equal(start.AddMinutes(30), session.ActivityData.Single().EndTimeUtc); // what was read keeps its real time
    }

    [Fact]
    public async Task BackfillReadingSessions_EachSessionThatFinishesAChapterCountsARead()
    {
        var f = await Setup();
        var chapter = await f.Context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series).SingleAsync(c => c.Id == f.ChapterId);
        var progress = new AppUserProgress
        {
            ChapterId = f.ChapterId,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            AppUserId = f.UserId,
            PagesRead = Pages,
            LastModifiedUtc = DateTime.UtcNow.AddDays(-9),
        };
        f.Context.AppUserProgresses.Add(progress);
        await f.Context.SaveChangesAsync();
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>()).Returns(progress);

        await f.Service.BackfillReadingSessions(f.UserId,
        [
            Entry(f.ChapterId, DayAtNoon(4), 10, startPage: 90, endPage: Pages),
            Entry(f.ChapterId, DayAtNoon(2), 10, startPage: 90, endPage: Pages),
            Entry(f.ChapterId, DayAtNoon(2).AddMinutes(12), 5, startPage: 1, endPage: Pages), // same sitting as the one before
        ]);

        Assert.Equal(2, (await f.Context.AppUserProgresses.AsNoTracking().SingleAsync()).TotalReads);
    }

    [Fact]
    public async Task BackfillReadingSessions_ProgressIsStampedWithWhenItWasReadSoLaterRequestsAreNotSkipped()
    {
        var f = await Setup();
        var chapter = await f.Context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series).SingleAsync(c => c.Id == f.ChapterId);
        // Like the real save, which stamps the row with the time of the upload
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>())
            .Returns(_ => f.Context.AppUserProgresses.AsNoTracking().FirstOrDefault(p => p.ChapterId == f.ChapterId));
        f.Reader.SaveReadingProgress(Arg.Any<ProgressDto>(), f.UserId, false, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var dto = call.Arg<ProgressDto>();
            var row = f.Context.AppUserProgresses.FirstOrDefault(p => p.ChapterId == f.ChapterId);
            if (row == null)
            {
                row = new AppUserProgress
                {
                    ChapterId = f.ChapterId,
                    VolumeId = chapter.VolumeId,
                    SeriesId = chapter.Volume.SeriesId,
                    LibraryId = chapter.Volume.Series.LibraryId,
                    AppUserId = f.UserId,
                };
                f.Context.AppUserProgresses.Add(row);
            }
            row.PagesRead = dto.PageNum;
            row.LastModifiedUtc = DateTime.UtcNow;
            f.Context.SaveChanges();
            return Task.FromResult(true);
        });

        var start = DayAtNoon(2);
        await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, start, 10, endPage: 10)]);
        var second = await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, start.AddMinutes(30), 10, startPage: 10, endPage: 25)]);

        Assert.Equal([BackfillReadingStatus.Applied], second);
        var stored = await f.Context.AppUserProgresses.AsNoTracking().SingleAsync();
        Assert.Equal(25, stored.PagesRead);
        Assert.Equal(start.AddMinutes(40), stored.LastReadUtc);
        // What devices syncing from the server go by is still when the progress was saved
        Assert.True(stored.LastModifiedUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task BackfillReadingSessions_ReadingOlderThanWhatAnEarlierRequestReportedDoesNotMoveProgressBack()
    {
        var f = await Setup();
        var chapter = await f.Context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series).SingleAsync(c => c.Id == f.ChapterId);
        var start = DayAtNoon(2);
        var row = new AppUserProgress
        {
            ChapterId = f.ChapterId,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            AppUserId = f.UserId,
            PagesRead = 25,
            LastReadUtc = start.AddMinutes(40),
        };
        f.Context.AppUserProgresses.Add(row);
        await f.Context.SaveChangesAsync();
        // Saved when it was sent, a day after it was read
        await f.Context.AppUserProgresses.ExecuteUpdateAsync(p => p
            .SetProperty(x => x.LastModifiedUtc, DateTime.UtcNow)
            .SetProperty(x => x.LastReadUtc, (DateTime?) start.AddMinutes(40)));
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>())
            .Returns(_ => f.Context.AppUserProgresses.AsNoTracking().First());

        await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, start, 10, endPage: 10)]);

        await f.Reader.DidNotReceiveWithAnyArgs().SaveReadingProgress(default!, default, default, default);
    }

    [Fact]
    public async Task BackfillReadingSessions_ProgressIsNotStampedWhenItCouldNotBeSaved()
    {
        var f = await Setup();
        f.Progress.GetUserProgressAsync(f.ChapterId, f.UserId, Arg.Any<CancellationToken>()).Returns((AppUserProgress?) null);
        f.Reader.SaveReadingProgress(Arg.Any<ProgressDto>(), f.UserId, false, Arg.Any<CancellationToken>()).Returns(false);

        var results = await f.Service.BackfillReadingSessions(f.UserId, [Entry(f.ChapterId, DayAtNoon(1), 10)]);

        Assert.Equal([BackfillReadingStatus.Applied], results);
        Assert.Empty(await f.Context.AppUserProgresses.ToListAsync());
    }

    [Fact]
    public async Task ChangingProgressClearsWhenItWasRead()
    {
        var f = await Setup();
        var chapter = await f.Context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series).SingleAsync(c => c.Id == f.ChapterId);
        var row = new AppUserProgress
        {
            ChapterId = f.ChapterId,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            AppUserId = f.UserId,
            PagesRead = 3,
            LastReadUtc = DateTime.UtcNow.AddDays(-2),
        };
        f.Context.AppUserProgresses.Add(row);
        await f.Context.SaveChangesAsync();
        Assert.NotNull((await f.Context.AppUserProgresses.AsNoTracking().SingleAsync()).LastReadUtc);

        row.PagesRead = 4;
        await f.Context.SaveChangesAsync();

        Assert.Null((await f.Context.AppUserProgresses.AsNoTracking().SingleAsync()).LastReadUtc);
    }
}
