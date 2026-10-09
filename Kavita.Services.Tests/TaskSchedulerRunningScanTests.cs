using System.Reflection;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.States;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class TaskSchedulerRunningScanTests
{
    public TaskSchedulerRunningScanTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void EnqueuedScan_CountsAsRunning()
    {
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(1, false, true));

        var enqueued = JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.ScanQueue, 0, int.MaxValue);
        Assert.True(Assert.Single(enqueued).Value.InEnqueuedState);
        Assert.True(TaskScheduler.RunningAnyTasksByMethod(TaskScheduler.ScanTasks, TaskScheduler.ScanQueue));
    }

    [Fact]
    public void ProcessingScan_CountsAsRunning()
    {
        var jobId = BackgroundJob.Enqueue<ScannerService>(s => s.ScanLibrary(1, false, true));
        // ProcessingState's constructor is internal, Hangfire only creates it from a worker
        var processing = (ProcessingState) Activator.CreateInstance(typeof(ProcessingState),
            BindingFlags.Instance | BindingFlags.NonPublic, null, ["server", "worker"], null)!;
        Assert.True(new BackgroundJobClient().ChangeState(jobId, processing, EnqueuedState.StateName));

        var enqueued = JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.ScanQueue, 0, int.MaxValue);
        Assert.DoesNotContain(enqueued, j => j.Value.InEnqueuedState);
        Assert.True(TaskScheduler.RunningAnyTasksByMethod(TaskScheduler.ScanTasks, TaskScheduler.ScanQueue));
    }

    [Fact]
    public void NoScan_NotRunning()
    {
        BackgroundJob.Enqueue<CleanupService>(s => s.CleanupCacheDirectory());

        Assert.False(TaskScheduler.RunningAnyTasksByMethod(TaskScheduler.ScanTasks, TaskScheduler.ScanQueue));
    }
}
