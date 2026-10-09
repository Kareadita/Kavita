using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class DelayedScanRetimeTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc);

    private static DelayedScanJob Job(string jobId, string method, object[] args, int createdMinutesAgo, TimeSpan runIn)
    {
        return new DelayedScanJob(jobId, method, args, Now.AddMinutes(-createdMinutesAgo), Now + runIn);
    }

    [Fact]
    public void OldestByCreatedAt_RunsInOneMinute()
    {
        var jobs = new[]
        {
            Job("41", "ScanLibrary", [1, false], 20, TimeSpan.FromHours(1)),
            Job("40", "ScanLibrary", [2, false], 28, TimeSpan.FromHours(2.9)),
            Job("42", "ScanLibrary", [3, false], 10, TimeSpan.FromHours(2)),
        };

        var plan = DelayedScanRetime.Plan(jobs, Now);

        Assert.Equal(Now.AddMinutes(1), plan.Moves.Single(m => m.JobId == "40").RunAtUtc);
        Assert.Empty(plan.Deletes);
    }

    [Fact]
    public void Others_MoveToThreeHoursAfterOldest()
    {
        var jobs = new[]
        {
            Job("40", "ScanLibrary", [2, false], 28, TimeSpan.FromHours(2.5)),
            Job("41", "ScanLibrary", [1, false], 20, TimeSpan.FromHours(2.8)),
            Job("42", "ScanSeries", [1, 812, true], 5, TimeSpan.FromMinutes(5)),
        };

        var plan = DelayedScanRetime.Plan(jobs, Now);

        var queuedAt = Now.AddMinutes(1).AddHours(3);
        Assert.Equal(queuedAt, plan.Moves.Single(m => m.JobId == "41").RunAtUtc);
        Assert.Equal(queuedAt, plan.Moves.Single(m => m.JobId == "42").RunAtUtc);
    }

    [Fact]
    public void SameTarget_KeepsOldestDeletesRest()
    {
        // A runs, then B, series D and B again are asked for while it does
        var jobs = new[]
        {
            Job("10", "ScanLibrary", [2, false], 30, TimeSpan.FromHours(2.5)),
            Job("11", "ScanSeries", [1, 812, true], 20, TimeSpan.FromHours(2.7)),
            Job("12", "ScanLibrary", [2, false], 10, TimeSpan.FromHours(2.8)),
        };

        var plan = DelayedScanRetime.Plan(jobs, Now);

        Assert.Equal(["12"], plan.Deletes);
        Assert.Equal(
            [new RetimeMove("10", Now.AddMinutes(1)), new RetimeMove("11", Now.AddMinutes(1).AddHours(3))],
            plan.Moves);
    }

    [Fact]
    public void OldestAlreadyDueSooner_IsNotPushedBack()
    {
        var jobs = new[] { Job("40", "ScanLibrary", [2, false], 28, TimeSpan.FromSeconds(20)) };

        var plan = DelayedScanRetime.Plan(jobs, Now);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void NoJobs_EmptyPlan()
    {
        Assert.True(DelayedScanRetime.Plan([], Now).IsEmpty);
    }
}
