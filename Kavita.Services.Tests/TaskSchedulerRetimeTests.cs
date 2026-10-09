using Hangfire;
using Hangfire.InMemory;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class TaskSchedulerRetimeTests
{
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();
    private readonly TaskScheduler _taskScheduler;

    public TaskSchedulerRetimeTests()
    {
        JobStorage.Current = new InMemoryStorage();

        _taskScheduler = new TaskScheduler(Substitute.For<ICacheService>(), Substitute.For<ILogger<TaskScheduler>>(),
            Substitute.For<IScannerService>(), Substitute.For<IUnitOfWork>(), Substitute.For<IMetadataService>(),
            Substitute.For<IBackupService>(), Substitute.For<ICleanupService>(), Substitute.For<IStatsService>(),
            Substitute.For<IVersionUpdaterService>(), Substitute.For<IWordCountAnalyzerService>(),
            Substitute.For<IMediaConversionService>(), Substitute.For<IScrobblingService>(),
            Substitute.For<ILicenseService>(), Substitute.For<IExternalMetadataService>(),
            Substitute.For<ISmartCollectionSyncService>(), Substitute.For<IWantToReadSyncService>(), _eventHub,
            Substitute.For<IEmailService>(), Substitute.For<IAuthKeyService>());
    }

    [Fact]
    public async Task ScanEnd_PullsOldestDelayedScanForward()
    {
        var parkedFor = TimeSpan.FromHours(3);
        var bookId = BackgroundJob.Schedule<TaskScheduler>(t => t.ScanLibrary(2, false), parkedFor);
        // CreatedAt must differ between jobs for the order to be defined
        Thread.Sleep(20);
        var seriesId = BackgroundJob.Schedule<TaskScheduler>(t => t.ScanSeries(1, 812, true), parkedFor);
        Thread.Sleep(20);
        BackgroundJob.Schedule<TaskScheduler>(t => t.ScanLibrary(2, false), parkedFor);

        var before = DateTime.UtcNow;
        await _taskScheduler.RetimeDelayedScans();
        var after = DateTime.UtcNow;

        var (scans, total) = TaskScheduler.GetScheduledScans(10);
        Assert.Equal(2, total);

        var book = scans[0];
        Assert.Equal(bookId, book.JobId);
        Assert.InRange(book.RunAtUtc, before.AddMinutes(1).AddSeconds(-1), after.AddMinutes(1).AddSeconds(1));

        var series = scans[1];
        Assert.Equal(seriesId, series.JobId);
        Assert.Equal(book.RunAtUtc.AddHours(3), series.RunAtUtc, TimeSpan.FromSeconds(1));

        await _eventHub.Received(1).SendMessageAsync(MessageFactory.ScanRescheduled,
            Arg.Is<SignalRMessageDto>(m => ((ScanRescheduledEventBodyDto) m.Body!).Scans.Count == 2));
    }

    [Fact]
    public async Task NoDelayedScans_SendsNothing()
    {
        await _taskScheduler.RetimeDelayedScans();

        await _eventHub.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!);
    }
}
