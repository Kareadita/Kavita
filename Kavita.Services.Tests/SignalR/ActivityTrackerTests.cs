using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.SignalR;
using Kavita.Services.SignalR;
using Xunit;

namespace Kavita.Services.Tests.SignalR;

public class ActivityTrackerTests
{
    private const string JobId = "6";
    private const string CorrelationId = "d8d37d43." + JobId;

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private readonly ManualTime _time = new();
    private readonly ActivityTracker _tracker;
    private readonly HashSet<string> _processing = [JobId];

    public ActivityTrackerTests()
    {
        _tracker = new ActivityTracker(_time);
    }

    private static SignalRMessageDto Folder(string folder, string eventType = ProgressEventType.Updated, string? correlationId = CorrelationId)
    {
        var message = MessageFactory.FileScanProgressEvent(folder, 1, "Manga", eventType);
        message.CorrelationId = correlationId;
        return message;
    }

    private static SignalRMessageDto Series(string eventType, string? correlationId = CorrelationId)
    {
        var message = MessageFactory.LibraryScanProgressEvent(1, "Manga", eventType, "One Piece", 3, 10);
        message.CorrelationId = correlationId;
        return message;
    }

    [Fact]
    public void Updates_KeepOnlyTheLatestMessage()
    {
        _tracker.Record(Folder("M:/A", ProgressEventType.Started));
        _tracker.Record(Folder("M:/B"));
        _tracker.Record(Folder("M:/C"));

        var running = _tracker.GetRunning(_processing);

        Assert.Single(running);
        Assert.Equal("M:/C", running[0].SubTitle);
    }

    [Fact]
    public void Ended_RemovesOnlyThatName()
    {
        _tracker.Record(Folder("M:/A", ProgressEventType.Started));
        _tracker.Record(Series(ProgressEventType.Started));

        _tracker.Record(Folder("M:/A", ProgressEventType.Ended));

        var running = _tracker.GetRunning(_processing);
        Assert.Single(running);
        Assert.Equal("ScanProgress", running[0].Name);
    }

    [Fact]
    public void SecondEnded_IsANoOp()
    {
        _tracker.Record(Series(ProgressEventType.Started));
        _tracker.Record(Series(ProgressEventType.Ended));
        _tracker.Record(Series(ProgressEventType.Ended));

        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void NoProgressMessage_IsNotTracked()
    {
        var cleanupOnHold = MessageFactory.CleanupOnHoldEvent();
        cleanupOnHold.CorrelationId = CorrelationId;

        _tracker.Record(cleanupOnHold);

        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void JobNoLongerProcessing_IsDropped()
    {
        _tracker.Record(Folder("M:/A", ProgressEventType.Started));

        Assert.Empty(_tracker.GetRunning(new HashSet<string>()));
        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void NoCorrelationId_DroppedOnceStale()
    {
        _tracker.Record(Folder("M:/A", ProgressEventType.Started, correlationId: null));

        _time.Advance(ActivityTracker.NoJobStaleAfter - TimeSpan.FromSeconds(1));
        Assert.Single(_tracker.GetRunning(_processing));

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void OverCap_EvictsTheLeastRecentlyUpdated()
    {
        var processing = new HashSet<string>();
        for (var i = 0; i <= ActivityTracker.MaxRows; i++)
        {
            processing.Add(i.ToString());
            _tracker.Record(Folder($"M:/{i}", ProgressEventType.Started, $"d8d37d43.{i}"));
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var running = _tracker.GetRunning(processing);

        Assert.Equal(ActivityTracker.MaxRows, running.Count);
        Assert.DoesNotContain(running, m => m.SubTitle == "M:/0");
        Assert.Equal("M:/1", running.First().SubTitle);
    }
}
