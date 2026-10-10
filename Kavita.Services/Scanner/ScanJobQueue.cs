using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

/// <summary>
/// All Hangfire access for scan jobs: queueing them, moving them, and <see cref="Read"/>ing what is queued. The only
/// code that knows method names and argument positions. Query the snapshot with ScanJobExtensions
/// </summary>
public static class ScanJobQueue
{
    private static readonly string[] Queues = [TaskScheduler.ScanQueue, TaskScheduler.DefaultQueue];
    private static readonly HashSet<string> FinishedStates = [SucceededState.StateName, DeletedState.StateName, FailedState.StateName];

    /// <summary>
    /// Ids of the jobs queued through this class that may still be waiting, per storage
    /// </summary>
    private static readonly ConditionalWeakTable<JobStorage, ConcurrentDictionary<string, byte>> QueuedIds = new();

    public static string Enqueue(Expression<Func<Task>> job) => Remember(BackgroundJob.Enqueue(job));

    public static string Enqueue<T>(Expression<Func<T, Task>> job) => Remember(BackgroundJob.Enqueue(job));

    public static string Schedule(Expression<Func<Task>> job, TimeSpan delay) => Remember(BackgroundJob.Schedule(job, delay));

    public static string Schedule(Expression<Func<Task>> job, DateTimeOffset runAt) => Remember(BackgroundJob.Schedule(job, runAt));

    /// <summary>
    /// Moves a delayed job. Nothing happens when it already left the Scheduled state
    /// </summary>
    public static void Reschedule(string jobId, DateTime runAtUtc)
    {
        BackgroundJob.Reschedule(jobId, new DateTimeOffset(runAtUtc), ScheduledState.StateName);
    }

    /// <summary>
    /// Deletes a delayed job. Nothing happens when it already left the Scheduled state
    /// </summary>
    public static void DeleteScheduled(string jobId)
    {
        BackgroundJob.Delete(jobId, ScheduledState.StateName);
    }

    public static ScanQueueSnapshot Read()
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var stored = ReadStoredJobs(monitoring);

        var jobs = stored
            .Select(j => ToScanJob(j, monitoring))
            .OfType<ScanJob>()
            .ToList();
        var folderRequests = stored
            .Where(j => j.State != ScanJobState.Processing && IsScanner(j.Job) && j.Job.Method.Name == nameof(IScannerService.ScanFolder))
            .Select(j => j.Job.Args[0])
            .OfType<ScanFolderRequest>()
            .ToList();

        return new ScanQueueSnapshot(jobs, folderRequests);
    }

    private static string Remember(string jobId)
    {
        QueuedIds.GetOrCreateValue(JobStorage.Current).TryAdd(jobId, 0);
        return jobId;
    }

    private static List<StoredJob> ReadStoredJobs(IMonitoringApi monitoring)
    {
        // Requests and ScanFolder have no [Queue], so they wait in the default queue
        var enqueued = Queues
            .SelectMany(queue => monitoring.EnqueuedJobs(queue, 0, int.MaxValue))
            // InMemoryStorage keeps a started job in its queue list
            .Where(j => j.Value.InEnqueuedState)
            .Select(j => new StoredJob(j.Key, j.Value.Job, ScanJobState.Enqueued, null));
        var scheduled = monitoring.ScheduledJobs(0, int.MaxValue)
            .Where(j => j.Value.InScheduledState)
            .Select(j => new StoredJob(j.Key, j.Value.Job, ScanJobState.Scheduled, DateTime.SpecifyKind(j.Value.EnqueueAt, DateTimeKind.Utc)));
        var processing = monitoring.ProcessingJobs(0, int.MaxValue)
            .Where(j => j.Value.InProcessingState)
            .Select(j => new StoredJob(j.Key, j.Value.Job, ScanJobState.Processing, null));

        var stored = enqueued.Concat(scheduled).Concat(processing)
            .Where(j => j.Job != null)
            .ToList();
        stored.AddRange(ReadTakenByWorker(stored.Select(j => j.JobId).ToHashSet()));

        return stored;
    }

    /// <summary>
    /// A worker takes a job off its queue a few ms before marking it Processing. In between, no list above shows it,
    /// so the jobs this class queued are looked up by id
    /// </summary>
    private static IEnumerable<StoredJob> ReadTakenByWorker(HashSet<string> listedIds)
    {
        var queuedIds = QueuedIds.GetOrCreateValue(JobStorage.Current);
        using var connection = JobStorage.Current.GetConnection();

        foreach (var jobId in queuedIds.Keys.Where(id => !listedIds.Contains(id)))
        {
            var data = connection.GetJobData(jobId);
            if (data?.Job == null || FinishedStates.Contains(data.State))
            {
                queuedIds.TryRemove(jobId, out _);
                continue;
            }

            if (data.State == EnqueuedState.StateName)
            {
                yield return new StoredJob(jobId, data.Job, ScanJobState.Enqueued, null);
            }
        }
    }

    private static ScanJob? ToScanJob(StoredJob stored, IMonitoringApi monitoring)
    {
        var job = stored.Job;
        if (IsScanner(job))
        {
            return job.Method.Name switch
            {
                nameof(IScannerService.ScanLibraries) => Create(stored, ScanTarget.AllLibraries, Arg<bool>(job, 0), false, monitoring),
                nameof(IScannerService.ScanLibrary) => Create(stored, ScanTarget.Library(Arg<int>(job, 0)), Arg<bool>(job, 1), false, monitoring),
                nameof(IScannerService.ScanSeries) => Create(stored, ScanTarget.Series(null, Arg<int>(job, 0)), Arg<bool>(job, 1), false, monitoring),
                _ => null,
            };
        }

        if (IsRequest(job))
        {
            return job.Method.Name switch
            {
                nameof(ITaskScheduler.EnqueueScanLibraries) => Create(stored, ScanTarget.AllLibraries, Arg<bool>(job, 0), true, monitoring),
                nameof(ITaskScheduler.EnqueueScanLibrary) => Create(stored, ScanTarget.Library(Arg<int>(job, 0)), Arg<bool>(job, 1), true, monitoring),
                nameof(ITaskScheduler.EnqueueScanSeries) => Create(stored, ScanTarget.Series(Arg<int>(job, 0), Arg<int>(job, 1)), Arg<bool>(job, 2), true, monitoring),
                _ => null,
            };
        }

        return null;
    }

    private static ScanJob Create(StoredJob stored, ScanTarget target, bool force, bool isRequest, IMonitoringApi monitoring)
    {
        var createdAt = monitoring.JobDetails(stored.JobId)?.CreatedAt;
        var createdAtUtc = createdAt.HasValue
            ? DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc)
            : stored.RunAtUtc ?? DateTime.UtcNow;

        return new ScanJob(stored.JobId, target, force, isRequest, stored.State, createdAtUtc, stored.RunAtUtc);
    }

    private static bool IsScanner(Job job) => job.Type.IsAssignableTo(typeof(IScannerService));
    private static bool IsRequest(Job job) => job.Type.IsAssignableTo(typeof(ITaskScheduler));

    private static T Arg<T>(Job job, int index) => (T) job.Args[index]!;

    private sealed record StoredJob(string JobId, Job Job, ScanJobState State, DateTime? RunAtUtc);
}
