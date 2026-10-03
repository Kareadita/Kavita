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
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started));
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/B"));
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/C"));

        var running = _tracker.GetRunning(_processing);

        Assert.Single(running);
        Assert.Equal("M:/C", running[0].SubTitle);
    }

    [Fact]
    public void Ended_RemovesOnlyThatName()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Started));

        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Ended));

        var running = _tracker.GetRunning(_processing);
        Assert.Single(running);
        Assert.Equal("ScanProgress", running[0].Name);
    }

    [Fact]
    public void SecondEnded_IsANoOp()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Started));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Ended));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Ended));

        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void NoProgressMessage_IsNotTracked()
    {
        var cleanupOnHold = MessageFactory.CleanupOnHoldEvent();
        cleanupOnHold.CorrelationId = CorrelationId;

        _tracker.Record(MessageFactory.NotificationProgress, cleanupOnHold);

        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void JobNoLongerProcessing_IsDropped()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started));

        Assert.Empty(_tracker.GetRunning(new HashSet<string>()));
        Assert.Empty(_tracker.GetRunning(_processing));
    }

    [Fact]
    public void NoCorrelationId_DroppedOnceStale()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started, correlationId: null));

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
            _tracker.Record(MessageFactory.NotificationProgress, Folder($"M:/{i}", ProgressEventType.Started, $"d8d37d43.{i}"));
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var running = _tracker.GetRunning(processing);

        Assert.Equal(ActivityTracker.MaxRows, running.Count);
        Assert.DoesNotContain(running, m => m.SubTitle == "M:/0");
        Assert.Equal("M:/1", running.First().SubTitle);
    }

    private static SignalRMessageDto Added(string? correlationId = CorrelationId)
    {
        var message = MessageFactory.SeriesAddedEvent(1, "One Piece", 1);
        message.CorrelationId = correlationId;
        return message;
    }

    private void RunScan()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started));
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Ended));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Updated));
        _tracker.Record(MessageFactory.SeriesAdded, Added());
        _tracker.Record(MessageFactory.SeriesAdded, Added());
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Ended));
    }

    [Fact]
    public void FinishedJob_IsRecentOnceNotProcessing()
    {
        RunScan();

        Assert.Empty(_tracker.GetRecentJobs(_processing));

        var job = Assert.Single(_tracker.GetRecentJobs(new HashSet<string>()));
        Assert.Equal(CorrelationId, job.CorrelationId);
        Assert.True(job.Completed);
        Assert.Equal(2, job.SeriesAdded);
        Assert.Equal(2, job.Steps.Count);
        Assert.All(job.Steps, s => Assert.NotEqual(ProgressEventType.Ended, s.EventType));
    }

    [Fact]
    public void JobThatThrew_IsNotCompleted()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Updated));

        var job = Assert.Single(_tracker.GetRecentJobs(new HashSet<string>()));
        Assert.False(job.Completed);
    }

    [Fact]
    public void StepThatStartsAgain_IsNotEnded()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Updated));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Ended));
        _tracker.Record(MessageFactory.NotificationProgress, Series(ProgressEventType.Started));

        Assert.False(_tracker.GetRecentJobs(new HashSet<string>())[0].Completed);
    }

    [Fact]
    public void NoCorrelationId_HasNoHistory()
    {
        _tracker.Record(MessageFactory.NotificationProgress, Folder("M:/A", ProgressEventType.Started, correlationId: null));

        Assert.Empty(_tracker.GetRecentJobs(new HashSet<string>()));
    }

    [Fact]
    public void History_DroppedAfterWindow()
    {
        RunScan();
        _tracker.Record(MessageFactory.Error, MessageFactory.ErrorEvent("Bad", "Worse"));

        _time.Advance(ActivityTracker.RecentWindow - TimeSpan.FromSeconds(1));
        Assert.Single(_tracker.GetRecentJobs(new HashSet<string>()));
        Assert.Single(_tracker.GetRecentEntries());

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(_tracker.GetRecentJobs(new HashSet<string>()));
        Assert.Empty(_tracker.GetRecentEntries());
    }

    [Fact]
    public void Entries_OnlyAdminBroadcasts_NewestFirst()
    {
        _tracker.Record(MessageFactory.Info, MessageFactory.InfoEvent("First", ""));
        _tracker.Record(MessageFactory.ScrobblingKeyExpired, MessageFactory.InfoEvent("Key", ""));
        _tracker.Record(MessageFactory.UpdateAvailable, MessageFactory.InfoEvent("Update", ""));
        _tracker.Record(MessageFactory.Error, MessageFactory.ErrorEvent("Second", ""));

        var entries = _tracker.GetRecentEntries();

        Assert.Equal(["Second", "Update", "First"], entries.Select(e => e.Title));
    }

    [Fact]
    public void Entries_OverCap_DropsOldest()
    {
        for (var i = 0; i <= ActivityTracker.MaxRecentEntries; i++)
        {
            _tracker.Record(MessageFactory.Info, MessageFactory.InfoEvent(i.ToString(), ""));
        }

        var entries = _tracker.GetRecentEntries();

        Assert.Equal(ActivityTracker.MaxRecentEntries, entries.Count);
        Assert.DoesNotContain(entries, e => e.Title == "0");
    }
}
