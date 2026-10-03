using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.SignalR;

public sealed class ProgressThrottle(ILogger<ProgressThrottle> logger, TimeProvider timeProvider, TimeSpan interval)
    : IProgressThrottle
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, JobState> _jobs = new();

    internal int TrackedCount => _jobs.Count;

    public ProgressThrottle(ILogger<ProgressThrottle> logger) : this(logger, TimeProvider.System, DefaultInterval)
    {
    }

    private sealed class JobState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public DateTimeOffset LastSent = DateTimeOffset.MinValue;
        public string? LastCode;
        public Func<Task>? Pending;
        public bool FlushScheduled;
    }

    public async Task SendAsync(SignalRMessageDto message, Func<Task> send)
    {
        var key = $"{message.Name}|{message.CorrelationId}";
        if (!_jobs.TryGetValue(key, out var job))
        {
            // A job that throws never sends ended, so its state is only cleaned up here
            EvictStale();
            job = _jobs.GetOrAdd(key, _ => new JobState());
        }

        TimeSpan flushIn;
        await job.Gate.WaitAsync();
        try
        {
            var now = timeProvider.GetUtcNow();
            var sendNow = message.EventType != ProgressEventType.Updated
                          || message.Code != job.LastCode
                          || now - job.LastSent >= interval;

            if (sendNow)
            {
                // Dropping the pending update matters on "ended": a later flush would put the job back on screen
                job.Pending = null;
                job.LastSent = now;
                job.LastCode = message.Code;
                await send();

                if (message.EventType == ProgressEventType.Ended) _jobs.TryRemove(key, out _);
                return;
            }

            job.Pending = send;
            if (job.FlushScheduled) return;

            job.FlushScheduled = true;
            flushIn = interval - (now - job.LastSent);
        }
        finally
        {
            job.Gate.Release();
        }

        _ = FlushAsync(job, flushIn);
    }

    private void EvictStale()
    {
        var staleBefore = timeProvider.GetUtcNow() - StaleAfter;
        foreach (var (key, job) in _jobs)
        {
            if (job.LastSent < staleBefore && !job.FlushScheduled) _jobs.TryRemove(key, out _);
        }
    }

    private async Task FlushAsync(JobState job, TimeSpan delay)
    {
        while (true)
        {
            await Task.Delay(delay, timeProvider);

            await job.Gate.WaitAsync();
            try
            {
                var pending = job.Pending;
                if (pending == null)
                {
                    job.FlushScheduled = false;
                    return;
                }

                // An immediate send (step change, interval elapsed) may have gone out while this flush was waiting
                var now = timeProvider.GetUtcNow();
                var sinceLast = now - job.LastSent;
                if (sinceLast < interval)
                {
                    delay = interval - sinceLast;
                    continue;
                }

                job.FlushScheduled = false;
                job.Pending = null;
                job.LastSent = now;
                await pending();
                return;
            }
            catch (Exception ex)
            {
                job.FlushScheduled = false;
                logger.LogDebug(ex, "A delayed progress update could not be sent");
                return;
            }
            finally
            {
                job.Gate.Release();
            }
        }
    }
}
