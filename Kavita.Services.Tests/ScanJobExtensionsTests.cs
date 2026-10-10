using Kavita.Services.Extensions;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class ScanJobExtensionsTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static ScanJob Scan(ScanTarget target, ScanJobState state, bool force = false) =>
        new("1", target, force, false, state, Now, state == ScanJobState.Scheduled ? Now.AddMinutes(30) : null);

    private static ScanJob Request(ScanTarget target, ScanJobState state, bool force = false) =>
        new("2", target, force, true, state, Now, state == ScanJobState.Scheduled ? Now.AddHours(3) : null);

    public static TheoryData<string, ScanTarget, ScanTarget, bool> CoversCases => new()
    {
        { "same series", ScanTarget.Series(1, 42), ScanTarget.Series(1, 42), true },
        { "series job without its library", ScanTarget.Series(null, 42), ScanTarget.Series(1, 42), true },
        { "other series", ScanTarget.Series(1, 43), ScanTarget.Series(1, 42), false },
        { "a series does not cover its library", ScanTarget.Series(1, 42), ScanTarget.Library(1), false },
        { "its library", ScanTarget.Library(1), ScanTarget.Series(1, 42), true },
        { "another library", ScanTarget.Library(2), ScanTarget.Series(1, 42), false },
        { "every library covers a series", ScanTarget.AllLibraries, ScanTarget.Series(1, 42), true },
        { "every library covers a library", ScanTarget.AllLibraries, ScanTarget.Library(1), true },
        { "a library does not cover every library", ScanTarget.Library(1), ScanTarget.AllLibraries, false },
    };

    [Theory]
    [MemberData(nameof(CoversCases))]
    public void Covers(string because, ScanTarget scan, ScanTarget target, bool expected)
    {
        Assert.True(expected == scan.Covers(target), because);
    }

    [Fact]
    public void FindWaiting_RunningScanNeverCounts()
    {
        var jobs = new[] { Scan(ScanTarget.Library(1), ScanJobState.Processing) };

        Assert.Null(jobs.FindWaiting(ScanTarget.Library(1)));
        Assert.Null(jobs.FindWaiting(ScanTarget.Series(1, 42)));
    }

    [Fact]
    public void FindWaiting_UnforcedDoesNotCoverForced()
    {
        var jobs = new[] { Scan(ScanTarget.Series(1, 42), ScanJobState.Enqueued) };

        Assert.NotNull(jobs.FindWaiting(ScanTarget.Series(1, 42)));
        Assert.Null(jobs.FindWaiting(ScanTarget.Series(1, 42), force: true));
    }

    [Fact]
    public void FindWaiting_StartedRequestDoesNotCount()
    {
        var jobs = new[] { Request(ScanTarget.Series(1, 42), ScanJobState.Processing) };

        Assert.Null(jobs.FindWaiting(ScanTarget.Series(1, 42)));
        Assert.NotNull(jobs.FindAlreadyRequested(ScanTarget.Series(1, 42)));
    }

    [Fact]
    public void FindAlreadyRequested_RunningScanNeverCounts()
    {
        var jobs = new[] { Scan(ScanTarget.Series(null, 42), ScanJobState.Processing) };

        Assert.Null(jobs.FindAlreadyRequested(ScanTarget.Series(1, 42)));
    }

    [Fact]
    public void IsScannerBusy_OnlyScansQueuedOrRunning()
    {
        Assert.True(new[] { Scan(ScanTarget.Library(1), ScanJobState.Enqueued) }.IsScannerBusy());
        Assert.True(new[] { Scan(ScanTarget.Library(1), ScanJobState.Processing) }.IsScannerBusy());
        Assert.False(new[] { Scan(ScanTarget.Library(1), ScanJobState.Scheduled) }.IsScannerBusy());
        Assert.False(new[] { Request(ScanTarget.Library(1), ScanJobState.Processing) }.IsScannerBusy());
    }

    [Fact]
    public void Delayed_OnlyScheduledRequests()
    {
        var delayed = Request(ScanTarget.Library(1), ScanJobState.Scheduled);
        var jobs = new[] { delayed, Request(ScanTarget.Library(2), ScanJobState.Enqueued), Scan(ScanTarget.Library(3), ScanJobState.Scheduled) };

        Assert.Equal([delayed], jobs.Delayed());
    }

    [Fact]
    public void HasScanDueBefore_ScheduledScansOnly()
    {
        var jobs = new[] { Scan(ScanTarget.Library(1), ScanJobState.Scheduled), Request(ScanTarget.Library(2), ScanJobState.Scheduled) };

        Assert.True(jobs.HasScanDueBefore(Now.AddHours(1)));
        Assert.False(jobs.HasScanDueBefore(Now.AddMinutes(10)));
    }

    [Fact]
    public void LibraryChecks_EveryLibraryScan()
    {
        var jobs = new[] { Scan(ScanTarget.AllLibraries, ScanJobState.Processing) };

        Assert.True(jobs.IsLibraryInUse(1));
        Assert.False(jobs.HasLibraryScan(1));
    }
}
