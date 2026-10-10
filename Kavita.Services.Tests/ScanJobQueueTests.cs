using Hangfire;
using Hangfire.InMemory;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Models.Scanner;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class ScanJobQueueTests
{
    public ScanJobQueueTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void EnqueuedScan_ScannerBusy()
    {
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(1, false, true));

        Assert.True(ScanJobQueue.Read().Jobs.IsScannerBusy());
    }

    [Fact]
    public void ProcessingScan_ScannerBusy()
    {
        var jobId = BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(1, false, true));
        HangfireJobs.MarkProcessing(jobId);

        var job = Assert.Single(ScanJobQueue.Read().Jobs);
        Assert.Equal(ScanJobState.Processing, job.State);
        Assert.True(new[] { job }.IsScannerBusy());
    }

    [Fact]
    public void ProcessingScanFolder_NotBusy()
    {
        var jobId = BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest("M:/Accel World", "M:/Accel World/v02.cbz", false)));
        HangfireJobs.MarkProcessing(jobId);

        Assert.False(ScanJobQueue.Read().Jobs.IsScannerBusy());
    }

    [Fact]
    public void OtherJob_NotAScanJob()
    {
        BackgroundJob.Enqueue<CleanupService>(s => s.CleanupCacheDirectory());

        Assert.Empty(ScanJobQueue.Read().Jobs);
    }

    [Fact]
    public void JobsQueuedEveryWay_AllMapped()
    {
        var instance = BackgroundJob.Enqueue<IScannerService>(s => s.ScanSeries(5, false));
        var generic = BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibraries(true));
        var request = BackgroundJob.Enqueue<ITaskScheduler>(t => t.EnqueueScanSeries(1, 6, false));
        var delayedRequest = BackgroundJob.Schedule<TaskScheduler>(t => t.EnqueueScanLibrary(2, true), TimeSpan.FromHours(3));
        var startedRequest = BackgroundJob.Enqueue<ITaskScheduler>(t => t.EnqueueScanLibrary(3, false));
        HangfireJobs.MarkProcessing(startedRequest);
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest("M:/Accel World", "M:/Accel World/v02.cbz", false)));

        var jobs = ScanJobQueue.Read().Jobs
            .OrderBy(j => int.Parse(j.JobId))
            .Select(j => (j.JobId, j.Target, j.Force, j.IsRequest, j.State))
            .ToList();

        Assert.Equal(
        [
            (instance, ScanTarget.Series(null, 5), false, false, ScanJobState.Enqueued),
            (generic, ScanTarget.AllLibraries, true, false, ScanJobState.Enqueued),
            (request, ScanTarget.Series(1, 6), false, true, ScanJobState.Enqueued),
            (delayedRequest, ScanTarget.Library(2), true, true, ScanJobState.Scheduled),
            (startedRequest, ScanTarget.Library(3), false, true, ScanJobState.Processing),
        ], jobs);
    }

    [Fact]
    public void TakenByWorkerBeforeProcessing_StillSeen()
    {
        var jobId = ScanJobQueue.Enqueue<ITaskScheduler>(t => t.EnqueueScanSeries(1, 6, false));
        using var connection = JobStorage.Current.GetConnection();
        using var fetched = connection.FetchNextJob([TaskScheduler.DefaultQueue], CancellationToken.None);

        var job = Assert.Single(ScanJobQueue.Read().Jobs);
        Assert.Equal((jobId, ScanJobState.Enqueued), (job.JobId, job.State));
    }

    [Fact]
    public void ScheduledScanFolder_KeepsRequestAndRunTime()
    {
        var request = new ScanFolderRequest("M:/Accel World", "M:/Accel World/v02.cbz", false);
        var before = DateTime.UtcNow;
        var jobId = BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(request), TimeSpan.FromSeconds(30));

        var job = Assert.Single(ScanJobQueue.Read().FolderJobs);
        Assert.Equal((jobId, request), (job.JobId, job.Request));
        Assert.InRange(job.RunAtUtc!.Value, before.AddSeconds(29), DateTime.UtcNow.AddSeconds(31));
    }

    [Fact]
    public void EnqueuedScanFolder_HasNoRunTime()
    {
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest("M:/Accel World", string.Empty, false)));

        Assert.Null(Assert.Single(ScanJobQueue.Read().FolderJobs).RunAtUtc);
    }

    [Theory]
    [InlineData(typeof(IScannerService), nameof(IScannerService.ScanLibraries), 1)]
    [InlineData(typeof(IScannerService), nameof(IScannerService.ScanLibrary), 3)]
    [InlineData(typeof(IScannerService), nameof(IScannerService.ScanSeries), 2)]
    [InlineData(typeof(IScannerService), nameof(IScannerService.ScanFolder), 1)]
    [InlineData(typeof(ITaskScheduler), nameof(ITaskScheduler.EnqueueScanLibraries), 1)]
    [InlineData(typeof(ITaskScheduler), nameof(ITaskScheduler.EnqueueScanLibrary), 2)]
    [InlineData(typeof(ITaskScheduler), nameof(ITaskScheduler.EnqueueScanSeries), 3)]
    public void MappedMethods_KeepTheParametersTheMapperReads(Type type, string method, int parameters)
    {
        Assert.True(parameters == type.GetMethod(method)!.GetParameters().Length,
            $"{type.Name}.{method} changed its parameters, update ScanJobQueue.ToScanJob and this test");
    }
}
