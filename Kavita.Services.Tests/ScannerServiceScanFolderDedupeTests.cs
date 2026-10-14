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
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class ScannerServiceScanFolderDedupeTests : AbstractDbTest
{
    private static readonly string AccelWorldFolder = Root + "Accel World";
    private static readonly string AccelWorldFile = AccelWorldFolder + "/Accel World v02.cbz";

    private readonly ITestOutputHelper _testOutputHelper;
    private int _libraryId;
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();

    public ScannerServiceScanFolderDedupeTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public async Task ScanFolder_LibraryScanQueued_RequestsNothing()
    {
        var (scanner, library, _) = await CreateServices();
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(library.Id, false, true));

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));

        Assert.Empty(SeriesScanRequests(SeriesId(library, "Accel World")));
    }

    [Fact]
    public async Task ScanFolder_DailyScanQueued_RequestsNothing()
    {
        var (scanner, library, _) = await CreateServices();
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibraries(false));

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));

        Assert.Empty(SeriesScanRequests(SeriesId(library, "Accel World")));
    }

    [Fact]
    public async Task ScanFolder_DelayedSeriesScan_RequestsNothing()
    {
        var (scanner, library, _) = await CreateServices();
        var seriesId = SeriesId(library, "Accel World");
        var delayedId = BackgroundJob.Schedule<TaskScheduler>(t => t.EnqueueScanSeries(library.Id, seriesId, true), TimeSpan.FromHours(3));

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));

        Assert.Equal(delayedId, Assert.Single(SeriesScanRequests(seriesId)));
    }

    [Fact]
    public async Task ScanFolder_SeriesScanRunning_RequestsOneFollowUp()
    {
        var (scanner, library, _) = await CreateServices();
        var seriesId = SeriesId(library, "Accel World");
        var runningId = BackgroundJob.Enqueue<ScannerService>(s => s.ScanSeries(seriesId, true));
        HangfireJobs.MarkProcessing(runningId);

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));
        await scanner.ScanFolder(AccelWorldChange(AccelWorldFolder + "/Accel World v03.cbz"));

        Assert.Single(SeriesScanRequests(seriesId));
    }

    [Fact]
    public async Task ScanFolder_TwoSeriesUnderOnePublisherFolder_RequestsBoth()
    {
        var (scanner, library, _) = await CreateServices();

        await scanner.ScanFolder(Change(Root + "YenPress", Root + "YenPress/Frieren/Frieren v02.cbz"));
        await scanner.ScanFolder(Change(Root + "YenPress", Root + "YenPress/Spy x Family/Spy x Family v02.cbz"));

        Assert.Single(SeriesScanRequests(SeriesId(library, "Frieren")));
        Assert.Single(SeriesScanRequests(SeriesId(library, "Spy x Family")));
    }

    [Theory]
    [InlineData("New Series", true, false)]
    [InlineData("New Series", false, true)]
    [InlineData("YenPress", true, false)]
    [InlineData("YenPress", false, true)]
    public async Task ScanFolder_FromApi_NoSeriesMatch_HonorsAbort(string folder, bool abort, bool expectLibraryScan)
    {
        var (scanner, library, taskScheduler) = await CreateServices();

        await ScanFolderFromApi(scanner, taskScheduler, Root + folder, abort);

        var jobs = ScanJobQueue.Read().Jobs;
        Assert.DoesNotContain(jobs, j => j.Target.SeriesId != null);
        Assert.Equal(expectLibraryScan, jobs.Any(j => j.Target == ScanTarget.Library(library.Id)));
    }

    [Theory]
    [InlineData("Solo", "Solo")]
    [InlineData("Dr. Stone", "Dr. Stone")]
    [InlineData("YenPress/Frieren", "Frieren")]
    [InlineData("YenPress/Frieren/Frieren v01.cbz", "Frieren")]
    public async Task ScanFolder_FromApi_AbortSet_ScansTheSeries(string path, string series)
    {
        var (scanner, library, taskScheduler) = await CreateServices();

        await ScanFolderFromApi(scanner, taskScheduler, Root + path, true);

        var request = Assert.Single(ScanJobQueue.Read().Jobs, j => j.Target.SeriesId != null);
        Assert.Equal(SeriesId(library, series), request.Target.SeriesId);
    }

    [Fact]
    public async Task ScanFolder_SameSeriesTwice_RequestsOne()
    {
        var (scanner, library, _) = await CreateServices();

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));
        await scanner.ScanFolder(AccelWorldChange(AccelWorldFolder + "/Accel World v03.cbz"));

        Assert.Single(SeriesScanRequests(SeriesId(library, "Accel World")));
    }

    [Fact]
    public async Task RequestScan_ParallelCalls_RequestOne()
    {
        var (scanner, library, _) = await CreateServices();
        var seriesId = SeriesId(library, "Accel World");

        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => scanner.RequestScan(ScanTarget.Series(library.Id, seriesId), AccelWorldFolder))));

        Assert.Single(SeriesScanRequests(seriesId));
    }

    [Fact]
    public async Task EnqueueScanFolder_SameChangeTwice_SchedulesOneJob()
    {
        var (_, _, taskScheduler) = await CreateServices();

        await taskScheduler.EnqueueScanFolderAsync(AccelWorldChange(AccelWorldFile), TimeSpan.FromSeconds(30));
        await taskScheduler.EnqueueScanFolderAsync(AccelWorldChange(AccelWorldFile), TimeSpan.FromSeconds(30));
        await taskScheduler.EnqueueScanFolderAsync(AccelWorldChange(AccelWorldFolder + "/Accel World v03.cbz"), TimeSpan.FromSeconds(30));

        Assert.Equal(2, ScanJobQueue.Read().FolderJobs.Count);
    }

    [Fact]
    public async Task EnqueueScanFolder_SendsScanRescheduled_OnlyForANewFolder()
    {
        var (_, _, taskScheduler) = await CreateServices();

        await taskScheduler.EnqueueScanFolderAsync(AccelWorldChange(AccelWorldFile), TimeSpan.FromSeconds(30));
        await taskScheduler.EnqueueScanFolderAsync(AccelWorldChange(AccelWorldFolder + "/Accel World v03.cbz"), TimeSpan.FromSeconds(30));
        Assert.Equal(1, ScanRescheduledCount());

        await taskScheduler.EnqueueScanFolderAsync(Change(Root + "YenPress", Root + "YenPress/Frieren/Frieren v02.cbz"),
            TimeSpan.FromSeconds(30));
        Assert.Equal(2, ScanRescheduledCount());
    }

    [Fact]
    public async Task ScanFolder_AnotherJobWaitsForTheFolder_SendsNothing()
    {
        var (scanner, _, _) = await CreateServices();
        var waiting = AccelWorldChange(AccelWorldFolder + "/Accel World v03.cbz");
        BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(waiting), TimeSpan.FromSeconds(30));

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));

        Assert.Equal(0, ScanRescheduledCount());
    }

    [Fact]
    public async Task ScanFolder_LastJobForTheFolder_SendsScanRescheduled()
    {
        var (scanner, _, _) = await CreateServices();
        var otherFolder = Change(Root + "YenPress", Root + "YenPress/Frieren/Frieren v02.cbz");
        BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(otherFolder), TimeSpan.FromSeconds(30));

        await scanner.ScanFolder(AccelWorldChange(AccelWorldFile));

        Assert.Equal(1, ScanRescheduledCount());
    }

    [Fact]
    public async Task EnqueueScanLibrary_LibraryScanRunning_QueuesFollowUp()
    {
        var (_, library, taskScheduler) = await CreateServices();
        var runningId = BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(library.Id, false, true));
        HangfireJobs.MarkProcessing(runningId);

        await taskScheduler.EnqueueScanLibrary(library.Id);

        var delayed = Assert.Single(ScanJobQueue.Read().Jobs.Delayed());
        Assert.Equal(ScanTarget.Library(library.Id), delayed.Target);
    }

    [Fact]
    public async Task EnqueueScanLibrary_LibraryScanWaiting_Skips()
    {
        var (_, library, taskScheduler) = await CreateServices();
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(library.Id, false, true));

        await taskScheduler.EnqueueScanLibrary(library.Id);

        Assert.Single(ScanJobQueue.Read().Jobs);
    }

    [Fact]
    public async Task EnqueueScanSeries_ForcedWhileUnforcedWaiting_StillQueues()
    {
        var (_, library, taskScheduler) = await CreateServices();
        var seriesId = SeriesId(library, "Accel World");
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanSeries(seriesId, false));

        await taskScheduler.EnqueueScanSeries(library.Id, seriesId, true);

        Assert.Single(ScanJobQueue.Read().Jobs, j => j.Force);
    }

    private async Task<(ScannerService Scanner, Library Library, TaskScheduler TaskScheduler)> CreateServices()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var library = new LibraryBuilder("Scan Folder", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder(Root).Build())
            .WithSeries(SeriesInFolder("Accel World", Root + "Accel World", Root + "Accel World"))
            .WithSeries(SeriesInFolder("Frieren", Root + "YenPress", Root + "YenPress/Frieren"))
            .WithSeries(SeriesInFolder("Spy x Family", Root + "YenPress", Root + "YenPress/Spy x Family"))
            .WithSeries(SeriesInFolder("Solo", Root + "Solo", Root + "Solo/Vol 1"))
            .WithSeries(SeriesInFolder("Dr. Stone", Root + "Dr. Stone", Root + "Dr. Stone"))
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var scanner = new ScannerHelper(unitOfWork, _testOutputHelper).CreateServices(eventHub: _eventHub);
        var taskScheduler = new TaskScheduler(Substitute.For<ICacheService>(), Substitute.For<ILogger<TaskScheduler>>(),
            scanner, unitOfWork, Substitute.For<IMetadataService>(),
            Substitute.For<IBackupService>(), Substitute.For<ICleanupService>(), Substitute.For<IStatsService>(),
            Substitute.For<IVersionUpdaterService>(), Substitute.For<IWordCountAnalyzerService>(),
            Substitute.For<IMediaConversionService>(), Substitute.For<IScrobblingService>(),
            Substitute.For<ILicenseService>(), Substitute.For<IExternalMetadataService>(),
            Substitute.For<ISmartCollectionSyncService>(), Substitute.For<IWantToReadSyncService>(), _eventHub,
            Substitute.For<IEmailService>(), Substitute.For<IAuthKeyService>());

        _libraryId = library.Id;
        return (scanner, library, taskScheduler);
    }

    private ScanFolderRequest AccelWorldChange(string changedPath) => Change(AccelWorldFolder, changedPath);

    private ScanFolderRequest Change(string folder, string changedPath) => new(_libraryId, folder, changedPath, true, false);

    private static async Task ScanFolderFromApi(ScannerService scanner, TaskScheduler taskScheduler, string path, bool abort)
    {
        await taskScheduler.EnqueueScanFolderFromApiAsync(path, abort);
        await scanner.ScanFolder(Assert.Single(ScanJobQueue.Read().FolderJobs).Request);
    }

    private int ScanRescheduledCount()
    {
        return _eventHub.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync)
                                                    && (string) c.GetArguments()[0]! == MessageFactory.ScanRescheduled);
    }

    private static int SeriesId(Library library, string name) => library.Series.Single(s => s.Name == name).Id;

    /// <summary>
    /// Ids of waiting jobs that will scan this series, as a scan or a scan request
    /// </summary>
    private static List<string> SeriesScanRequests(int seriesId)
    {
        return ScanJobQueue.Read().Jobs
            .Where(j => j.IsWaiting && j.Target.SeriesId == seriesId)
            .Select(j => j.JobId)
            .ToList();
    }

    private static Series SeriesInFolder(string name, string folderPath, string lowestFolderPath)
    {
        var series = new SeriesBuilder(name).WithFormat(MangaFormat.Archive).Build();
        series.FolderPath = folderPath;
        series.LowestFolderPath = lowestFolderPath;
        return series;
    }
}
