using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.SignalR;
using Kavita.Services.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kavita.Services.Tests.SignalR;

public class ProgressThrottleTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AfterFlush = TimeSpan.FromMilliseconds(400);

    private readonly ProgressThrottle _throttle = new(NullLogger<ProgressThrottle>.Instance, TimeProvider.System, Interval);
    private readonly ConcurrentQueue<SignalRMessageDto> _sent = new();

    private Task Send(SignalRMessageDto message) => _throttle.SendAsync(message, () =>
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    });

    private static SignalRMessageDto Folder(int current, string eventType = ProgressEventType.Updated,
        string? code = MessageEventCode.ScanListingFolders) =>
        MessageFactory.FileScanProgressEvent($"M:/{current}", 1, "Manga", eventType, code, current, 100);

    [Fact]
    public async Task Burst_SendsFirstImmediately_ThenOnlyTheNewest()
    {
        for (var i = 1; i <= 100; i++) await Send(Folder(i));

        Assert.Single(_sent);
        Assert.Equal("M:/1", _sent.First().SubTitle);

        await Task.Delay(AfterFlush);

        Assert.Equal(2, _sent.Count);
        Assert.Equal("M:/100", _sent.Last().SubTitle);
    }

    [Fact]
    public async Task Ended_DropsPendingUpdate()
    {
        await Send(Folder(0, ProgressEventType.Started, code: null));
        await Send(Folder(1, code: null));
        await Send(Folder(2, ProgressEventType.Ended, code: null));

        await Task.Delay(AfterFlush);

        Assert.Equal([ProgressEventType.Started, ProgressEventType.Ended], _sent.Select(m => m.EventType));
    }

    [Fact]
    public async Task StepChange_IsSentImmediately()
    {
        await Send(Folder(1, code: MessageEventCode.ScanListingFolders));
        await Send(Folder(1, code: MessageEventCode.ScanReadingFiles));

        Assert.Equal([MessageEventCode.ScanListingFolders, MessageEventCode.ScanReadingFiles], _sent.Select(m => m.Code));
    }

    [Fact]
    public async Task DifferentJobs_AreThrottledSeparately()
    {
        await Send(Folder(1));
        await Send(MessageFactory.BackupDatabaseProgressEvent(0.5f, "Copying Fonts"));

        Assert.Equal(2, _sent.Count);
    }

    [Fact]
    public async Task LateFlush_AfterAnImmediateSend_WaitsOutTheInterval()
    {
        var clock = new ManualTimeProvider();
        var throttle = new ProgressThrottle(NullLogger<ProgressThrottle>.Instance, clock, Interval);
        var sent = new ConcurrentQueue<(string Folder, DateTimeOffset At)>();
        Task Send(SignalRMessageDto m) => throttle.SendAsync(m, () =>
        {
            sent.Enqueue((m.SubTitle, clock.GetUtcNow()));
            return Task.CompletedTask;
        });

        await Send(Folder(1));
        await Send(Folder(2));                  // pending, delayed send due at +100ms

        clock.Advance(Interval);                // due, but the delayed send has not woken up yet
        await Send(Folder(3));                  // interval elapsed: goes out immediately, drops 2
        await Send(Folder(4));                  // pending again

        await clock.FireDueTimersAsync();       // the late delayed send wakes up

        Assert.Equal(["M:/1", "M:/3"], sent.Select(x => x.Folder));

        clock.Advance(Interval);
        await clock.FireDueTimersAsync();

        Assert.Equal(["M:/1", "M:/3", "M:/4"], sent.Select(x => x.Folder));
        Assert.Equal(Interval, sent.Last().At - sent.ElementAt(1).At);
    }

    [Fact]
    public async Task JobThatNeverEnds_IsEvictedOnceStale()
    {
        var clock = new ManualTimeProvider();
        var throttle = new ProgressThrottle(NullLogger<ProgressThrottle>.Instance, clock, Interval);
        Task Send(SignalRMessageDto m) => throttle.SendAsync(m, () => Task.CompletedTask);

        await Send(Folder(1, ProgressEventType.Started, code: null));
        Assert.Equal(1, throttle.TrackedCount);

        clock.Advance(ProgressThrottle.StaleAfter + TimeSpan.FromSeconds(1));
        await Send(MessageFactory.BackupDatabaseProgressEvent(0f, "Starting"));

        Assert.Equal(1, throttle.TrackedCount);
    }

    [Fact]
    public async Task UpdatesSpacedOut_AreAllSent()
    {
        for (var i = 1; i <= 3; i++)
        {
            await Send(Folder(i));
            await Task.Delay(Interval * 2);
        }

        Assert.Equal(3, _sent.Count);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            lock (_timers) _timers.Add(timer);
            return timer;
        }

        public async Task FireDueTimersAsync()
        {
            List<ManualTimer> due;
            lock (_timers)
            {
                due = _timers.Where(t => t.DueAt <= _now).ToList();
                _timers.RemoveAll(due.Contains);
            }
            foreach (var timer in due) timer.Fire();

            // Let the awaiting flush continuation run
            await Task.Delay(50);
        }

        private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
        {
            public DateTimeOffset DueAt { get; } = dueAt;
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
