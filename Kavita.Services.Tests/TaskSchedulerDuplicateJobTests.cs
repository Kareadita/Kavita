using System.Runtime.CompilerServices;
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
using Kavita.Models.DTOs.Settings;
using Kavita.Services.Metadata;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class TaskSchedulerDuplicateJobTests
{
    public TaskSchedulerDuplicateJobTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void ScanSeries_SameArgs_Matches()
    {
        var wordCount = Instance<IWordCountAnalyzerService, WordCountAnalyzerService>();
        BackgroundJob.Enqueue(() => wordCount.ScanSeries(1, 2, false, CancellationToken.None));

        Assert.True(TaskScheduler.HasAlreadyEnqueuedTask<IWordCountAnalyzerService>(s => s.ScanSeries(1, 2, false, CancellationToken.None)));
    }

    [Fact]
    public void GenerateCoversForSeries_SameArgs_Matches()
    {
        var metadata = Instance<IMetadataService, MetadataService>();
        BackgroundJob.Enqueue(() => metadata.GenerateCoversForSeries(Settings(), 1, 2, false, false, CancellationToken.None));

        Assert.True(TaskScheduler.HasAlreadyEnqueuedTask<IMetadataService>(s => s.GenerateCoversForSeries(Settings(), 1, 2, false, false, CancellationToken.None)));
    }

    [Fact]
    public void DifferentArgs_DoesNotMatch()
    {
        var wordCount = Instance<IWordCountAnalyzerService, WordCountAnalyzerService>();
        BackgroundJob.Enqueue(() => wordCount.ScanSeries(1, 2, false, CancellationToken.None));

        Assert.False(TaskScheduler.HasAlreadyEnqueuedTask<IWordCountAnalyzerService>(s => s.ScanSeries(1, 3, false, CancellationToken.None)));
    }

    [Fact]
    public void RefreshMetadata_QueuedForcedRefresh_CoversUnforcedRequest()
    {
        var metadata = Instance<IMetadataService, MetadataService>();
        BackgroundJob.Enqueue(() => metadata.GenerateCoversForLibrary(1, true, false, CancellationToken.None));

        CreateTaskScheduler(metadata).RefreshMetadata(1, false, false);

        Assert.Equal(1, LibraryRefreshCount());
    }

    [Fact]
    public void RefreshMetadata_QueuedUnforcedRefresh_DoesNotCoverForcedRequest()
    {
        var metadata = Instance<IMetadataService, MetadataService>();
        BackgroundJob.Enqueue(() => metadata.GenerateCoversForLibrary(1, false, false, CancellationToken.None));

        CreateTaskScheduler(metadata).RefreshMetadata(1, true, false);

        Assert.Equal(2, LibraryRefreshCount());
    }

    private static TaskScheduler CreateTaskScheduler(IMetadataService metadataService)
    {
        return new TaskScheduler(Substitute.For<ICacheService>(), Substitute.For<ILogger<TaskScheduler>>(),
            Substitute.For<IScannerService>(), Substitute.For<IUnitOfWork>(), metadataService,
            Substitute.For<IBackupService>(), Substitute.For<ICleanupService>(), Substitute.For<IStatsService>(),
            Substitute.For<IVersionUpdaterService>(), Substitute.For<IWordCountAnalyzerService>(),
            Substitute.For<IMediaConversionService>(), Substitute.For<IScrobblingService>(),
            Substitute.For<ILicenseService>(), Substitute.For<IExternalMetadataService>(),
            Substitute.For<ISmartCollectionSyncService>(), Substitute.For<IWantToReadSyncService>(), Substitute.For<IEventHub>(),
            Substitute.For<IEmailService>(), Substitute.For<IAuthKeyService>());
    }

    private static int LibraryRefreshCount()
    {
        return JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.DefaultQueue, 0, int.MaxValue)
            .Count(j => j.Value.Job?.Method.Name == nameof(IMetadataService.GenerateCoversForLibrary));
    }

    // An NSubstitute proxy type cannot be loaded back from storage, DI hands Hangfire the real class
    private static TService Instance<TService, TImplementation>() =>
        (TService) RuntimeHelpers.GetUninitializedObject(typeof(TImplementation));

    private static ServerSettingDto Settings() => new() { CacheDirectory = "cache", TaskScan = "daily" };
}
